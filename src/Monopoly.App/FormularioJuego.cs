using System;
using System.Drawing;
using System.Windows.Forms;
using Monopoly.App.Estilo;
using Monopoly.Core.Estructuras;
using Monopoly.Core.Logica;
using Monopoly.Core.Modelo;
using Monopoly.Core.Red;

namespace Monopoly.App;

/// <summary>
/// Ventana principal de la partida: el tablero a la izquierda y, a la derecha, los botones de historial y
/// de opciones (engranaje), las tarjetas de los jugadores, lo que se espera ahora, solo las acciones que
/// corresponden a la fase y la actividad reciente (plegable). Todo lo que muestra proviene de los mensajes
/// del servidor; los botones solo envían solicitudes (el servidor valida igualmente). Los dados se lanzan
/// con el botón físico y los pagos se hacen con la tarjeta: aquí no hay botones para eso.
/// </summary>
internal sealed class FormularioJuego : Form
{
    private const int MaximoLineasRegistro = 300;
    private const int AnchoLateral = 460;
    private const int Margen = 14;

    private readonly SesionJuego _sesion;
    private readonly Tablero _tablero = new Tablero();
    private readonly PanelTablero _panelTablero = new PanelTablero { Dock = DockStyle.Fill };
    private readonly Lienzo _lateral = new Lienzo { Dock = DockStyle.Right, Width = AnchoLateral };
    private readonly PanelJugadores _panelJugadores = new PanelJugadores();
    private readonly BotonIcono _btnHistorial = new BotonIcono(Iconos.Libro, "Historial de transacciones") { Text = "Historial" };
    private readonly BotonIcono _btnOpciones = new BotonIcono(Iconos.Engranaje, "Opciones");
    private readonly TarjetaPanel _tarjetaSituacion = new TarjetaPanel { Radio = 14f };
    private readonly Etiqueta _lblTurno = new Etiqueta(string.Empty, 10f, FontStyle.Bold, Tema.Rojo);
    private readonly Etiqueta _lblSituacion = new Etiqueta(string.Empty, 11f, FontStyle.Bold, Tema.Tinta);
    private readonly BotonRedondeado _btnComprar = new BotonRedondeado { Text = "Comprar", Estilo = EstiloBoton.Exito };
    private readonly BotonRedondeado _btnNoComprar = new BotonRedondeado { Text = "No comprar", Estilo = EstiloBoton.Secundario };
    private readonly BotonRedondeado _btnTerminar = new BotonRedondeado { Text = "Terminar turno", Estilo = EstiloBoton.Principal };
    private readonly BotonRedondeado _btnRetirar = new BotonRedondeado { Text = "Retirar jugadores desconectados...", Estilo = EstiloBoton.Secundario };
    private readonly BotonRedondeado _btnActividad = new BotonRedondeado { Estilo = EstiloBoton.Secundario, Radio = 10f };
    private readonly ListBox _lstRegistro = new ListBox { IntegralHeight = false, BorderStyle = BorderStyle.None };
    private readonly ColaCircular<string> _registro = new ColaCircular<string>(MaximoLineasRegistro + 1);
    private readonly Etiqueta _lblUltimoEvento = new Etiqueta(string.Empty, 9.5f);
    private readonly Notificaciones _notificaciones;
    private FormularioHistorial? _historial;
    private FormularioOpciones? _opciones;
    private bool _actividadDesplegada = true;
    private bool _animarProximoEstado;
    private bool _finMostrado;
    private bool _desconectado;

    /// <summary>
    /// Crea la ventana de juego para una sesión.
    /// </summary>
    public FormularioJuego(SesionJuego sesion)
    {
        _sesion = sesion;
        AutoScaleMode = AutoScaleMode.None;
        Text = $"{Tema.NombreJuego} — {sesion.Nombre}{(sesion.EsOrganizador ? " (banco)" : string.Empty)}";
        StartPosition = FormStartPosition.CenterScreen;
        // Tamaño inicial: aprovecha la pantalla sin pasarse del área de trabajo.
        Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
        ClientSize = new Size(Math.Min(1720, area.Width - 40), Math.Min(1080, area.Height - 60));
        MinimumSize = new Size(Math.Min(1100, area.Width), Math.Min(780, area.Height));
        BackColor = Tema.Crema;
        Font = Tema.Texto(10f);
        _notificaciones = new Notificaciones(this);

        Controls.Add(_panelTablero);
        Controls.Add(_lateral);
        _lateral.BackColor = Tema.Crema;
        _lateral.Paint += (s, e) =>
        {
            using Pen borde = new Pen(Tema.Borde, 1f);
            e.Graphics.DrawLine(borde, 0, 0, 0, _lateral.Height);
        };

        _btnHistorial.Size = new Size(160, 48);
        _btnOpciones.Size = new Size(48, 48);
        _lateral.Controls.Add(_btnHistorial);
        _lateral.Controls.Add(_btnOpciones);
        _lateral.Controls.Add(_panelJugadores);

        _lblTurno.TextAlign = ContentAlignment.MiddleLeft;
        _tarjetaSituacion.Controls.Add(_lblTurno);
        _tarjetaSituacion.Controls.Add(_lblSituacion);
        _lateral.Controls.Add(_tarjetaSituacion);

        foreach (BotonRedondeado boton in new[] { _btnComprar, _btnNoComprar, _btnTerminar, _btnRetirar })
        {
            boton.Font = Tema.Texto(12f, FontStyle.Bold);
            _lateral.Controls.Add(boton);
        }

        _btnRetirar.Font = Tema.Texto(10f, FontStyle.Bold);
        _btnActividad.Font = Tema.Texto(10f, FontStyle.Bold);
        _lateral.Controls.Add(_btnActividad);
        _lstRegistro.Font = Tema.Texto(9.5f);
        _lstRegistro.BackColor = Tema.Marfil;
        _lstRegistro.ForeColor = Tema.TintaSuave;
        _lateral.Controls.Add(_lstRegistro);
        _lateral.Controls.Add(_lblUltimoEvento);

        _btnComprar.Click += (s, e) => Solicitar(cliente => cliente.ComprarPropiedad());
        _btnNoComprar.Click += (s, e) => Solicitar(cliente => cliente.NoComprar());
        _btnTerminar.Click += (s, e) => Solicitar(cliente => cliente.TerminarTurno());
        _btnRetirar.Click += (s, e) => RetirarDesconectados();
        _btnHistorial.Click += (s, e) => AbrirHistorial();
        _btnOpciones.Click += (s, e) => AbrirOpciones();
        _btnActividad.Click += (s, e) =>
        {
            _actividadDesplegada = !_actividadDesplegada;
            Reubicar();
        };
        _lateral.Resize += (s, e) => Reubicar();

        _sesion.EstadoActualizado += AlActualizarEstado;
        _sesion.EventoRecibido += AlRecibirEvento;
        _sesion.DadosRecibidos += AlRecibirDados;
        _sesion.ErrorRecibido += AlRecibirError;
        _sesion.FinRecibido += AlRecibirFin;
        _sesion.Desconectado += AlDesconectar;
        if (_sesion.MonitorCajero != null)
        {
            _sesion.MonitorCajero.PicoPerdida += AlPerderPico;
        }

        _sesion.RecorrerEventos(AgregarAlRegistro);
        HabilitarAcciones(null);
        if (_sesion.UltimoEstado != null)
        {
            AlActualizarEstado(_sesion.UltimoEstado);
        }

        Reubicar();
    }

    /// <summary>
    /// Indica si la ventana se cerró para volver al inicio y reconectarse.
    /// </summary>
    public bool VolverParaReconectar { get; private set; }

    /// <inheritdoc/>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        EstadoRed? estado = _sesion.UltimoEstado;
        if (!_desconectado && e.CloseReason == CloseReason.UserClosing && estado?.Instantanea.Estado == EstadoPartida.EnCurso)
        {
            string aviso = _sesion.EsOrganizador
                ? "Usted aloja al banco: si sale, la partida terminará para todos los jugadores. ¿Desea salir?"
                : "La partida sigue en curso. Podrá volver a entrar con el mismo nombre. ¿Desea salir?";
            if (MessageBox.Show(this, aviso, "Salir de la partida", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }

        base.OnFormClosing(e);
    }

    /// <inheritdoc/>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _sesion.EstadoActualizado -= AlActualizarEstado;
        _sesion.EventoRecibido -= AlRecibirEvento;
        _sesion.DadosRecibidos -= AlRecibirDados;
        _sesion.ErrorRecibido -= AlRecibirError;
        _sesion.FinRecibido -= AlRecibirFin;
        _sesion.Desconectado -= AlDesconectar;
        if (_sesion.MonitorCajero != null)
        {
            _sesion.MonitorCajero.PicoPerdida -= AlPerderPico;
        }

        _historial?.Close();
        _opciones?.Close();
        _notificaciones.Dispose();
        base.OnFormClosed(e);
    }

    /// <summary>
    /// Coloca los elementos de la columna derecha, de arriba hacia abajo, según el estado y el tamaño.
    /// </summary>
    private void Reubicar()
    {
        int ancho = _lateral.ClientSize.Width - (2 * Margen);
        int y = Margen;
        _btnOpciones.Location = new Point(_lateral.ClientSize.Width - Margen - _btnOpciones.Width, y);
        _btnHistorial.Location = new Point(_btnOpciones.Left - 8 - _btnHistorial.Width, y);
        y += _btnOpciones.Height + 8;

        int jugadores = Math.Max(1, _sesion.UltimoEstado?.Instantanea.Jugadores.Length ?? 1);
        _panelJugadores.SetBounds(Margen - 8, y, ancho + 16, (jugadores * (PanelJugadores.AltoTarjeta + PanelJugadores.Separacion)) + PanelJugadores.Separacion + 4);
        y = _panelJugadores.Bottom + 4;

        // El alto de la situación depende del texto (hasta 4 líneas).
        int anchoTexto = ancho + (2 * TarjetaPanel.MargenSombra) - 44;
        int altoTexto;
        using (Graphics g = CreateGraphics())
        {
            altoTexto = (int)Math.Ceiling(g.MeasureString(_lblSituacion.Text.Length == 0 ? " " : _lblSituacion.Text, _lblSituacion.Font, anchoTexto).Height) + 4;
        }

        altoTexto = Math.Min(altoTexto, (int)(_lblSituacion.Font.GetHeight() * 4) + 6);
        _tarjetaSituacion.SetBounds(Margen - TarjetaPanel.MargenSombra, y, ancho + (2 * TarjetaPanel.MargenSombra), altoTexto + 62);
        _lblTurno.SetBounds(22, 10, anchoTexto, 28);
        _lblSituacion.SetBounds(22, 40, anchoTexto, altoTexto);
        y = _tarjetaSituacion.Bottom + 4;

        // Solo los botones que corresponden a la fase.
        if (_btnComprar.Visible && _btnNoComprar.Visible)
        {
            int mitad = (ancho - 8) / 2;
            _btnComprar.SetBounds(Margen - 4, y, mitad + 8, 60);
            _btnNoComprar.SetBounds(Margen + mitad + 4, y, mitad + 8, 60);
            y += 62;
        }
        else if (_btnNoComprar.Visible)
        {
            _btnNoComprar.SetBounds(Margen - 4, y, ancho + 8, 60);
            y += 62;
        }

        if (_btnTerminar.Visible)
        {
            _btnTerminar.SetBounds(Margen - 4, y, ancho + 8, 60);
            y += 62;
        }

        if (_btnRetirar.Visible)
        {
            _btnRetirar.SetBounds(Margen - 4, y, ancho + 8, 50);
            y += 52;
        }

        // Actividad reciente, plegable.
        y += 4;
        _btnActividad.Text = "Actividad reciente" + (_actividadDesplegada ? "  ·  ocultar" : "  ·  mostrar");
        _btnActividad.SetBounds(Margen - 4, y, ancho + 8, 46);
        y += 48;
        int resto = _lateral.ClientSize.Height - y - Margen;
        _lstRegistro.Visible = _actividadDesplegada && resto > 40;
        _lblUltimoEvento.Visible = !_lstRegistro.Visible;
        if (_lstRegistro.Visible)
        {
            _lstRegistro.SetBounds(Margen, y, ancho, resto);
        }
        else
        {
            _lblUltimoEvento.SetBounds(Margen + 4, y, ancho - 8, Math.Max(24, Math.Min(44, resto)));
        }

        _lateral.Invalidate();
    }

    private void AlActualizarEstado(EstadoRed estado)
    {
        _panelTablero.MostrarEstado(estado, _animarProximoEstado);
        _animarProximoEstado = false;
        _panelJugadores.MostrarEstado(estado, _sesion.IdJugador);
        HabilitarAcciones(estado);
        DescribirSituacion(estado);
        _panelTablero.Aviso = AvisoParaTodos(estado);
        Reubicar();
    }

    private void AlRecibirDados(int idJugador, int dado1, int dado2)
    {
        // El ESTADO que sigue a los dados trae el recorrido: se anima casilla por casilla.
        _animarProximoEstado = true;
    }

    private void AlRecibirEvento(string texto)
    {
        AgregarAlRegistro(texto);
        if (texto.StartsWith("Cajero:", StringComparison.Ordinal))
        {
            // Lo que pasa en el cajero físico (tarjeta leída, botón ignorado...) se destaca para todos.
            bool rechazo = texto.Contains("rechaz", StringComparison.OrdinalIgnoreCase) || texto.Contains("no está registrada", StringComparison.Ordinal)
                || texto.Contains("pertenece a", StringComparison.Ordinal) || texto.Contains("desconectado", StringComparison.OrdinalIgnoreCase);
            _notificaciones.Mostrar(texto.Substring("Cajero:".Length).Trim(), rechazo ? TipoNotificacion.Error : TipoNotificacion.Informacion, 5000);
        }
    }

    private void AlRecibirError(string mensaje)
    {
        _notificaciones.Mostrar(mensaje, TipoNotificacion.Error, 6000);
        AgregarAlRegistro("⚠ " + mensaje);
    }

    private void AlPerderPico()
    {
        BeginInvoke(new Action(() => MessageBox.Show(this,
            "Se perdió la conexión con el cajero (Pico W).\n\nLa partida queda en pausa: nadie puede tirar los dados ni pagar " +
            "hasta reconectarla. Revise el cable USB y abra las opciones (engranaje) para pulsar \"Conectar\" o \"Detectar\". La partida no se pierde.",
            "Cajero desconectado", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
    }

    private void AlRecibirFin(string ganador, string resumen)
    {
        HabilitarAcciones(_sesion.UltimoEstado);
        Reubicar();
        if (_finMostrado)
        {
            return;
        }

        _finMostrado = true;
        BeginInvoke(new Action(() =>
        {
            using FormularioFin fin = new FormularioFin(_sesion.UltimoEstado, ganador, resumen, _sesion.IdJugador);
            fin.ShowDialog(this);
        }));
    }

    private void AlDesconectar(string motivo)
    {
        _desconectado = true;
        HabilitarAcciones(null);
        Reubicar();
        _lblTurno.Text = "Desconectado";
        _lblSituacion.ForeColor = Tema.Error;
        _lblSituacion.Text = motivo;
        AgregarAlRegistro("⚠ " + motivo);
        if (_sesion.EsOrganizador || _sesion.Cliente.CerradoPorElServidor
            || _sesion.UltimoEstado?.Instantanea.Estado == EstadoPartida.Finalizada)
        {
            MessageBox.Show(this, motivo, "Conexión cerrada", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Un jugador que pierde la conexión sigue en la partida: puede volver con el mismo nombre.
        string pregunta = motivo + "\n\nUsted sigue en la partida. ¿Desea volver a la pantalla de inicio para reconectarse " +
                          "con el mismo nombre?";
        if (MessageBox.Show(this, pregunta, "Conexión perdida", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
        {
            VolverParaReconectar = true;
            Close();
        }
    }

    private void RetirarDesconectados()
    {
        EstadoRed? estado = _sesion.UltimoEstado;
        if (estado == null)
        {
            return;
        }

        string nombres = string.Empty;
        foreach (int id in estado.IdsDesconectados)
        {
            EstadoJugador? jugador = estado.BuscarJugador(id);
            if (jugador != null && jugador.Activo)
            {
                nombres += (nombres.Length > 0 ? ", " : string.Empty) + jugador.Nombre;
            }
        }

        if (nombres.Length == 0)
        {
            return;
        }

        string pregunta = $"Se retirará de la partida a: {nombres}.\nSus propiedades volverán a estar libres y no podrán volver a jugar. ¿Continuar?";
        if (MessageBox.Show(this, pregunta, "Retirar jugadores desconectados", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        foreach (int id in estado.IdsDesconectados)
        {
            if (estado.BuscarJugador(id)?.Activo == true)
            {
                Solicitar(cliente => cliente.RetirarJugador(id));
            }
        }
    }

    private void Solicitar(Action<Cliente> solicitud)
    {
        _sesion.Solicitar(solicitud);
    }

    private void HabilitarAcciones(EstadoRed? estado)
    {
        InstantaneaJuego? i = estado?.Instantanea;
        int? yo = _sesion.IdJugador;
        bool enCurso = !_desconectado && i?.Estado == EstadoPartida.EnCurso;
        bool activo = yo.HasValue && (estado?.BuscarJugador(yo.Value)?.Activo ?? false);
        bool miTurno = enCurso && activo && i!.IdJugadorEnTurno == yo;

        // Los dados se lanzan con el botón físico y se paga o compra acercando la tarjeta al lector:
        // aquí solo aparecen las decisiones del jugador en turno que corresponden a la fase.
        _btnComprar.Visible = miTurno && i!.Fase == FaseTurno.EsperandoDecisionCompra;
        _btnNoComprar.Visible = miTurno && (i!.Fase == FaseTurno.EsperandoDecisionCompra || i.Fase == FaseTurno.EsperandoTarjetaCompra);
        _btnNoComprar.Text = miTurno && i!.Fase == FaseTurno.EsperandoTarjetaCompra ? "Cancelar compra (No comprar)" : "No comprar";
        _btnTerminar.Visible = miTurno && i!.Fase == FaseTurno.PuedeTerminar;
        _btnHistorial.Enabled = !_desconectado;

        // El organizador puede retirar a jugadores desconectados para que la partida no quede esperándolos.
        bool hayDesconectados = false;
        if (enCurso && estado != null)
        {
            foreach (int id in estado.IdsDesconectados)
            {
                hayDesconectados |= estado.BuscarJugador(id)?.Activo == true;
            }
        }

        _btnRetirar.Visible = _sesion.EsOrganizador && hayDesconectados;
        bool debo = enCurso && yo.HasValue && i!.IdDeudor == yo;
        _tarjetaSituacion.Resaltada = miTurno || debo;
        _tarjetaSituacion.ColorResaltado = Tema.Dorado;
        _tarjetaSituacion.ColorFondo = miTurno || debo ? Color.FromArgb(255, 249, 226) : Tema.Marfil;
    }

    private void DescribirSituacion(EstadoRed estado)
    {
        InstantaneaJuego i = estado.Instantanea;
        _lblSituacion.ForeColor = Tema.Tinta;
        if (i.Estado == EstadoPartida.Finalizada)
        {
            _lblTurno.Text = "Partida finalizada";
            _lblSituacion.Text = $"Ganador: {estado.BuscarJugador(i.IdGanador ?? 0)?.Nombre}.";
            return;
        }

        if (i.Estado != EstadoPartida.EnCurso || !i.IdJugadorEnTurno.HasValue)
        {
            _lblTurno.Text = string.Empty;
            _lblSituacion.Text = "Esperando el inicio de la partida.";
            return;
        }

        bool miTurno = i.IdJugadorEnTurno == _sesion.IdJugador;
        _lblTurno.Text = $"TURNO {i.NumeroTurno} DE {i.MaximoTurnos}{(miTurno ? "  ·  ¡ES SU TURNO!" : string.Empty)}";
        _lblSituacion.Text = AvisoParaTodos(estado);
    }

    /// <summary>
    /// Aviso de lo que se espera ahora, igual para todas las pantallas (se muestra también en el tablero).
    /// </summary>
    private string AvisoParaTodos(EstadoRed estado)
    {
        InstantaneaJuego i = estado.Instantanea;
        if (i.Estado != EstadoPartida.EnCurso || !i.IdJugadorEnTurno.HasValue)
        {
            return string.Empty;
        }

        int idEnTurno = i.IdJugadorEnTurno.Value;
        string nombre = estado.BuscarJugador(idEnTurno)?.Nombre ?? "?";
        bool miTurno = idEnTurno == _sesion.IdJugador;
        if (!estado.EstaConectado(idEnTurno) && !miTurno)
        {
            string ayuda = _sesion.EsOrganizador ? "Espere o retírelo con el botón de abajo." : "Espere a que vuelva o lo retire el organizador.";
            return $"{nombre} está DESCONECTADO. {ayuda}";
        }

        bool esperaCajero = i.Fase == FaseTurno.EsperandoDados || i.Fase == FaseTurno.EsperandoPago || i.Fase == FaseTurno.EsperandoTarjetaCompra;
        if (estado.EnPausaPorCajero && esperaCajero)
        {
            return "⏸ Partida en pausa: el cajero (Pico W) está desconectado. Esperando a que el organizador lo reconecte.";
        }

        string pruebas = estado.ModoSinHardware ? " (modo pruebas: el organizador lo simula)" : string.Empty;
        Propiedad? propiedad = i.IdPropiedadEnVenta.HasValue ? _tablero.BuscarPropiedad(i.IdPropiedadEnVenta.Value) : null;
        string precio = propiedad == null ? string.Empty : Formato.Dinero(propiedad.PrecioCompra);
        switch (i.Fase)
        {
            case FaseTurno.EsperandoDados:
                return $"Turno de {nombre}: presione el botón físico para lanzar los dados{pruebas}.";
            case FaseTurno.EsperandoDecisionCompra:
                return miTurno
                    ? $"¿Desea comprar {propiedad?.Nombre} por {precio}?"
                    : $"{nombre} decide si compra {propiedad?.Nombre} por {precio}.";
            case FaseTurno.EsperandoTarjetaCompra:
                return $"{nombre} quiere comprar {propiedad?.Nombre} por {precio}: acerque su tarjeta al lector{pruebas}.";
            case FaseTurno.EsperandoPago:
                return $"{i.DescripcionPagoPendiente}: acerque su tarjeta al lector{pruebas}.";
            default:
                return miTurno ? "Puede terminar su turno." : $"{nombre} puede terminar su turno.";
        }
    }

    /// <summary>
    /// Guarda la línea en la cola circular del registro (descarta la más antigua al pasar del máximo) y
    /// vuelve a mostrar la cola en la lista: el ListBox solo muestra, no almacena.
    /// </summary>
    private void AgregarAlRegistro(string texto)
    {
        _registro.Encolar(texto);
        while (_registro.Cantidad > MaximoLineasRegistro)
        {
            _registro.Desencolar();
        }

        _lstRegistro.BeginUpdate();
        _lstRegistro.Items.Clear();
        _registro.Recorrer(linea => _lstRegistro.Items.Add(linea));
        _lstRegistro.EndUpdate();
        _lstRegistro.TopIndex = Math.Max(0, _registro.Cantidad - 1);
        _lblUltimoEvento.Text = texto;
    }

    private void AbrirHistorial()
    {
        if (_historial == null || _historial.IsDisposed)
        {
            _historial = new FormularioHistorial(_sesion);
            _historial.Show(this);
        }
        else
        {
            _historial.Activate();
        }
    }

    private void AbrirOpciones()
    {
        if (_opciones == null || _opciones.IsDisposed)
        {
            _opciones = new FormularioOpciones(_sesion, Close);
            _opciones.Show(this);
        }
        else
        {
            _opciones.Activate();
        }
    }
}
