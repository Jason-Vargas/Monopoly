using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Monopoly.App.Estilo;
using Monopoly.Core.Estructuras;
using Monopoly.Core.Hardware;
using Monopoly.Core.Logica;
using Monopoly.Core.Red;

namespace Monopoly.App;

/// <summary>
/// Sala de espera: cada jugador conectado aparece como una tarjeta con su ficha, color, nombre y el estado
/// de su tarjeta RFID; a la derecha, la dirección del banco (con botón para copiarla), el estado de la
/// Raspberry Pi Pico W y, para el organizador, la vinculación de tarjetas. Solo el organizador ve
/// "Iniciar partida", deshabilitado con una explicación mientras falten jugadores o tarjetas.
/// </summary>
internal sealed class FormularioSalaEspera : Form
{
    private const int AnchoVentana = 1000;
    private const int ColumnaDerecha = 628;
    private const int AnchoDerecha = 356;

    private readonly SesionJuego _sesion;
    private readonly TarjetaJugadorSala[] _tarjetas = new TarjetaJugadorSala[Juego.MaximoJugadores];
    private readonly TarjetaPanel _tarjetaPico = new TarjetaPanel { Titulo = "Cajero (Pico W)", TamanioTitulo = 13f };
    private readonly Etiqueta _lblPico = new Etiqueta(string.Empty, 9f);
    private readonly ListBox _lstEventos = new ListBox { IntegralHeight = false, BorderStyle = BorderStyle.None };
    private readonly Etiqueta _lblEstado = new Etiqueta(string.Empty, 10.5f, FontStyle.Bold, Tema.Tinta);
    private readonly BotonRedondeado _btnIniciar = new BotonRedondeado { Text = "Iniciar partida", Estilo = EstiloBoton.Exito };
    private readonly BotonRedondeado _btnVincular = new BotonRedondeado { Text = "Vincular tarjeta" };
    private readonly BotonRedondeado _btnCancelarVinculacion = new BotonRedondeado { Text = "Cancelar", Estilo = EstiloBoton.Secundario };
    private readonly Etiqueta _lblVinculacion = new Etiqueta(string.Empty, 9f);
    private readonly Notificaciones _notificaciones;
    private readonly Timer _animacion = new Timer { Interval = 16 };
    private readonly string _direccionPrincipal;
    private readonly string _otrasDirecciones;
    private readonly BotonIcono _btnOpciones = new BotonIcono(Iconos.Engranaje, "Opciones");
    private FormularioOpciones? _opciones;
    private EstadoRed? _estado;
    private int? _seleccionado;
    private int? _esperandoTarjetaDe;
    private bool _partidaIniciada;
    private bool _primerEstado = true;

    /// <summary>
    /// Crea la sala de espera de una sesión ya aceptada por el servidor.
    /// </summary>
    public FormularioSalaEspera(SesionJuego sesion)
    {
        _sesion = sesion;
        AutoScaleMode = AutoScaleMode.None;
        Text = $"Sala de espera — {sesion.Nombre} · {Tema.NombreJuego}";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Tema.VerdeMenta;
        Font = Tema.Texto(10f);
        DoubleBuffered = true;
        _notificaciones = new Notificaciones(this);
        (_direccionPrincipal, _otrasDirecciones) = Direcciones(sesion);

        // Engranaje de opciones: la Pico W (solo el organizador, que la tiene conectada), el sonido y la partida.
        _btnOpciones.SetBounds(AnchoVentana - 62, 14, 46, 46);
        _btnOpciones.Click += (s, e) => AbrirOpciones();
        Controls.Add(_btnOpciones);
        if (sesion.MonitorCajero != null)
        {
            sesion.MonitorCajero.PicoPerdida += AlPerderPico;
        }

        int alto = (sesion.EsOrganizador ? 670 : 600);
        ClientSize = new Size(AnchoVentana, alto);
        int yBarraInferior = alto - 72;

        // Jugadores: cuadrícula de 2 × 2.
        for (int i = 0; i < _tarjetas.Length; i++)
        {
            TarjetaJugadorSala tarjeta = new TarjetaJugadorSala { Seleccionable = sesion.EsOrganizador };
            tarjeta.SetBounds(16 + ((i % 2) * 302), 84 + ((i / 2) * 136), 300, 134);
            tarjeta.Click += (s, e) => SeleccionarJugador(tarjeta.Jugador?.Id);
            _tarjetas[i] = tarjeta;
            Controls.Add(tarjeta);
        }

        // Actividad (registro de eventos).
        TarjetaPanel actividad = new TarjetaPanel { Titulo = "Actividad", TamanioTitulo = 12f };
        int yActividad = 356;
        actividad.SetBounds(16, yActividad, 604, yBarraInferior - yActividad - 4);
        _lstEventos.BackColor = Tema.Marfil;
        _lstEventos.ForeColor = Tema.TintaSuave;
        _lstEventos.Font = Tema.Texto(9.5f);
        _lstEventos.SetBounds(24, 62, actividad.Width - 48, actividad.Height - 80);
        actividad.Controls.Add(_lstEventos);
        Controls.Add(actividad);

        // Dirección del banco, con botón para copiarla.
        TarjetaPanel direccion = new TarjetaPanel
        {
            Titulo = sesion.EsOrganizador ? "Dirección del banco" : "Conectado al banco",
            TamanioTitulo = 13f,
            Icono = (g, a) => Iconos.Red(g, a, Tema.VerdeProfundo),
        };
        direccion.SetBounds(ColumnaDerecha, 76, AnchoDerecha, 170);
        direccion.Paint += (s, e) => DibujarDireccion(e.Graphics);
        BotonRedondeado copiar = new BotonRedondeado { Text = "Copiar", Estilo = EstiloBoton.Secundario, Radio = 10f };
        copiar.Font = Tema.Texto(9.5f, FontStyle.Bold);
        copiar.SetBounds(AnchoDerecha - 130, 112, 112, 42);
        copiar.Click += (s, e) => CopiarDireccion();
        direccion.Controls.Add(copiar);
        Controls.Add(direccion);

        // Estado de la Pico W.
        _tarjetaPico.Icono = (g, a) => Iconos.Chip(g, a, ColorPico());
        _tarjetaPico.SetBounds(ColumnaDerecha, 250, AnchoDerecha, 102);
        _lblPico.SetBounds(26, 60, AnchoDerecha - 50, 28);
        _tarjetaPico.Controls.Add(_lblPico);
        Controls.Add(_tarjetaPico);

        if (sesion.EsOrganizador)
        {
            // Registro de tarjetas: el organizador elige un jugador y la próxima tarjeta leída queda vinculada.
            TarjetaPanel rfid = new TarjetaPanel { Titulo = "Tarjetas RFID", TamanioTitulo = 13f, Icono = (g, a) => Iconos.TarjetaRfid(g, a, Tema.Rojo) };
            rfid.SetBounds(ColumnaDerecha, 356, AnchoDerecha, yBarraInferior - (356) - 4);
            Etiqueta ayuda = new Etiqueta("Haga clic en un jugador, pulse \"Vincular\" y acerque su tarjeta al lector.", 9f);
            ayuda.SetBounds(26, 58, AnchoDerecha - 52, 48);
            _btnVincular.SetBounds(20, 106, 196, 48);
            _btnVincular.Click += (s, e) => VincularTarjetaSeleccionado();
            _btnCancelarVinculacion.SetBounds(220, 106, 116, 48);
            _btnCancelarVinculacion.Click += (s, e) => CancelarVinculacion();
            _lblVinculacion.SetBounds(26, 158, AnchoDerecha - 52, rfid.Height - 176);
            rfid.Controls.Add(ayuda);
            rfid.Controls.Add(_btnVincular);
            rfid.Controls.Add(_btnCancelarVinculacion);
            rfid.Controls.Add(_lblVinculacion);
            Controls.Add(rfid);
        }

        // Barra inferior: explicación y "Iniciar partida".
        _lblEstado.SetBounds(24, yBarraInferior + 8, sesion.EsOrganizador ? 600 : AnchoVentana - 48, 52);
        _lblEstado.TextAlign = ContentAlignment.MiddleLeft;
        Controls.Add(_lblEstado);
        _btnIniciar.SetBounds(ColumnaDerecha, yBarraInferior + 4, AnchoDerecha, 60);
        _btnIniciar.Font = Tema.Texto(12.5f, FontStyle.Bold);
        _btnIniciar.Visible = sesion.EsOrganizador;
        _btnIniciar.Enabled = false;
        _btnIniciar.Click += (s, e) => _sesion.Solicitar(cliente => cliente.IniciarPartida());
        Controls.Add(_btnIniciar);

        _animacion.Tick += (s, e) => AnimarTarjetas();
        _sesion.EstadoActualizado += AlActualizarEstado;
        _sesion.EventoRecibido += AlRecibirEvento;
        _sesion.ErrorRecibido += AlRecibirError;
        _sesion.Desconectado += AlDesconectar;
        _sesion.CajeroCambiado += ActualizarCajero;
        _sesion.RecorrerEventos(texto => _lstEventos.Items.Add(texto));
        ActualizarCajero();
    }

    /// <summary>
    /// La partida comenzó: hay que pasar a la ventana de juego.
    /// </summary>
    public event Action? PartidaIniciada;

    /// <summary>
    /// Indica si la ventana se cerró para volver al inicio y reconectarse.
    /// </summary>
    public bool VolverParaReconectar { get; private set; }

    /// <inheritdoc/>
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_sesion.UltimoEstado != null)
        {
            AlActualizarEstado(_sesion.UltimoEstado);
        }
    }

    /// <inheritdoc/>
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Dibujo.Calidad(g);
        Dibujo.FondoTablero(g, ClientRectangle);
        Logotipo.Dibujar(g, new RectangleF(18, 12, 250, 56));

        using (Font titulo = Tema.Titulo(21f))
        using (SolidBrush rojo = new SolidBrush(Tema.Rojo))
        {
            g.DrawString("Sala de espera", titulo, rojo, 286, 4);
        }

        int cantidad = _estado?.Instantanea.Jugadores.Length ?? 0;
        using Font subtitulo = Tema.Texto(10.5f, FontStyle.Bold);
        using SolidBrush verde = new SolidBrush(Tema.VerdeProfundo);
        g.DrawString($"Jugadores conectados: {cantidad} de {Juego.MaximoJugadores}  ·  se necesitan al menos {Juego.MinimoJugadores}",
            subtitulo, verde, 292, 52);
    }

    /// <inheritdoc/>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _sesion.EstadoActualizado -= AlActualizarEstado;
        _sesion.EventoRecibido -= AlRecibirEvento;
        _sesion.ErrorRecibido -= AlRecibirError;
        _sesion.Desconectado -= AlDesconectar;
        _sesion.CajeroCambiado -= ActualizarCajero;
        if (_sesion.MonitorCajero != null)
        {
            _sesion.MonitorCajero.PicoPerdida -= AlPerderPico;
        }

        _opciones?.Close();
        _animacion.Dispose();
        _notificaciones.Dispose();
        base.OnFormClosed(e);
    }

    private void AlActualizarEstado(EstadoRed estado)
    {
        InstantaneaJuego instantanea = estado.Instantanea;
        if (instantanea.Estado != EstadoPartida.EsperandoJugadores)
        {
            // La partida ya comenzó (o el jugador se reconectó a una partida en curso).
            if (!_partidaIniciada)
            {
                _partidaIniciada = true;
                PartidaIniciada?.Invoke();
            }

            return;
        }

        EstadoRed? anterior = _estado;
        _estado = estado;

        // Si llegó la tarjeta del jugador que se estaba vinculando, termina la espera.
        if (_esperandoTarjetaDe.HasValue && estado.BuscarJugador(_esperandoTarjetaDe.Value)?.TieneTarjetaFisica == true
            && anterior?.BuscarJugador(_esperandoTarjetaDe.Value)?.TieneTarjetaFisica != true)
        {
            string nombre = estado.BuscarJugador(_esperandoTarjetaDe.Value)!.Nombre;
            _esperandoTarjetaDe = null;
            _lblVinculacion.ForeColor = Tema.Exito;
            _lblVinculacion.Text = $"Tarjeta vinculada a {nombre}.";
            _notificaciones.Mostrar($"Tarjeta vinculada a {nombre}.", TipoNotificacion.Exito);
            _seleccionado = PrimeroSinTarjeta(estado);
        }

        if (_seleccionado.HasValue && estado.BuscarJugador(_seleccionado.Value) == null)
        {
            _seleccionado = null;
        }

        if (_sesion.EsOrganizador && !_seleccionado.HasValue && !_esperandoTarjetaDe.HasValue)
        {
            _seleccionado = PrimeroSinTarjeta(estado);
        }

        ActualizarTarjetas(!_primerEstado);
        _primerEstado = false;
        ActualizarVinculacion();
        ActualizarCajero();
        ActualizarInicio();
        Invalidate(new Rectangle(0, 0, ColumnaDerecha, 80));
    }

    private void ActualizarTarjetas(bool animar)
    {
        EstadoJugador[] jugadores = _estado?.Instantanea.Jugadores ?? new EstadoJugador[0];
        bool hayAnimacion = false;
        for (int i = 0; i < _tarjetas.Length; i++)
        {
            TarjetaJugadorSala tarjeta = _tarjetas[i];
            EstadoJugador? jugador = i < jugadores.Length ? jugadores[i] : null;
            tarjeta.EsOrganizador = i == 0 && jugador != null;
            tarjeta.EsUsted = jugador != null && jugador.Id == _sesion.IdJugador;
            tarjeta.Conectado = jugador == null || _estado!.EstaConectado(jugador.Id);
            tarjeta.Seleccionada = jugador != null && _sesion.EsOrganizador && jugador.Id == _seleccionado;
            tarjeta.EsperandoTarjeta = jugador != null && jugador.Id == _esperandoTarjetaDe;
            tarjeta.MostrarJugador(jugador, animar);
            hayAnimacion |= tarjeta.Animando;
        }

        if (hayAnimacion)
        {
            _animacion.Start();
        }
    }

    private void AnimarTarjetas()
    {
        bool sigue = false;
        foreach (TarjetaJugadorSala tarjeta in _tarjetas)
        {
            if (tarjeta.Animando)
            {
                sigue = true;
                tarjeta.Invalidate();
            }
        }

        if (!sigue)
        {
            _animacion.Stop();
            foreach (TarjetaJugadorSala tarjeta in _tarjetas)
            {
                tarjeta.Invalidate();
            }
        }
    }

    private void ActualizarInicio()
    {
        if (_estado == null)
        {
            return;
        }

        InstantaneaJuego instantanea = _estado.Instantanea;
        int cantidad = instantanea.Jugadores.Length;
        bool cantidadValida = cantidad >= Juego.MinimoJugadores && cantidad <= Juego.MaximoJugadores;

        // En modo hardware todos necesitan una tarjeta física vinculada (el servidor también lo exige).
        string sinTarjeta = string.Empty;
        if (!_estado.ModoSinHardware)
        {
            foreach (EstadoJugador j in instantanea.Jugadores)
            {
                if (!j.TieneTarjetaFisica)
                {
                    sinTarjeta += (sinTarjeta.Length > 0 ? ", " : string.Empty) + j.Nombre;
                }
            }
        }

        _btnIniciar.Enabled = _sesion.EsOrganizador && cantidadValida && sinTarjeta.Length == 0;
        string organizador = cantidad > 0 ? instantanea.Jugadores[0].Nombre : "el organizador";
        if (!_sesion.EsOrganizador)
        {
            _lblEstado.ForeColor = Tema.VerdeProfundo;
            _lblEstado.Text = $"Esperando a que {organizador} inicie la partida...";
        }
        else if (!cantidadValida)
        {
            _lblEstado.ForeColor = Tema.Advertencia;
            _lblEstado.Text = $"Faltan jugadores: se necesitan al menos {Juego.MinimoJugadores}. Comparta la dirección del banco.";
        }
        else if (sinTarjeta.Length > 0)
        {
            _lblEstado.ForeColor = Tema.Advertencia;
            _lblEstado.Text = $"Falta vincular la tarjeta RFID de: {sinTarjeta}.";
        }
        else
        {
            _lblEstado.ForeColor = Tema.Exito;
            _lblEstado.Text = "Todo listo: puede iniciar la partida cuando estén todos.";
        }
    }

    private void AlRecibirEvento(string texto)
    {
        _lstEventos.Items.Add(texto);
        _lstEventos.TopIndex = Math.Max(0, _lstEventos.Items.Count - 1);
    }

    private void AlRecibirError(string mensaje)
    {
        _notificaciones.Mostrar(mensaje, TipoNotificacion.Error, 6000);
        if (_esperandoTarjetaDe.HasValue)
        {
            // Por ejemplo, "La tarjeta X ya está vinculada a Beto": la espera terminó sin vincular.
            _esperandoTarjetaDe = null;
            _lblVinculacion.ForeColor = Tema.Error;
            _lblVinculacion.Text = mensaje;
            ActualizarTarjetas(false);
            ActualizarVinculacion();
        }
    }

    private void SeleccionarJugador(int? id)
    {
        if (!_sesion.EsOrganizador || !id.HasValue || _esperandoTarjetaDe.HasValue)
        {
            return;
        }

        _seleccionado = id;
        ActualizarTarjetas(false);
        ActualizarVinculacion();
    }

    private void VincularTarjetaSeleccionado()
    {
        EstadoJugador? jugador = _seleccionado.HasValue ? _estado?.BuscarJugador(_seleccionado.Value) : null;
        if (jugador == null)
        {
            return;
        }

        _esperandoTarjetaDe = jugador.Id;
        _lblVinculacion.ForeColor = Tema.Informacion;
        _lblVinculacion.Text = $"Acerque al lector la tarjeta de {jugador.Nombre}...";
        _sesion.Solicitar(cliente => cliente.VincularTarjeta(jugador.Id));
        ActualizarTarjetas(false);
        ActualizarVinculacion();
    }

    private void CancelarVinculacion()
    {
        _esperandoTarjetaDe = null;
        _lblVinculacion.Text = string.Empty;
        _sesion.Solicitar(cliente => cliente.VincularTarjeta(0));
        ActualizarTarjetas(false);
        ActualizarVinculacion();
    }

    private bool PicoConectadaAqui => _sesion.Cajero.EsFisico && _sesion.Cajero.Estado == EstadoCajero.Conectado;

    private void ActualizarVinculacion()
    {
        if (!_sesion.EsOrganizador)
        {
            return;
        }

        bool picoConectada = PicoConectadaAqui;
        if (!picoConectada && _esperandoTarjetaDe.HasValue)
        {
            _esperandoTarjetaDe = null;
            _lblVinculacion.Text = string.Empty;
            ActualizarTarjetas(false);
        }

        EstadoJugador? seleccionado = _seleccionado.HasValue ? _estado?.BuscarJugador(_seleccionado.Value) : null;
        _btnVincular.Enabled = picoConectada && seleccionado != null && !_esperandoTarjetaDe.HasValue;
        _btnVincular.Text = seleccionado != null ? $"Vincular a {Corto(seleccionado.Nombre)}" : "Vincular tarjeta";
        _btnCancelarVinculacion.Enabled = _esperandoTarjetaDe.HasValue;
        if (_esperandoTarjetaDe.HasValue)
        {
            return;
        }

        if (!picoConectada)
        {
            _lblVinculacion.ForeColor = Tema.TintaSuave;
            _lblVinculacion.Text = _sesion.ModoSinHardware
                ? "En modo sin hardware no hace falta vincular tarjetas."
                : "Conecte la Pico W en las opciones (engranaje) o marque \"Modo sin hardware\".";
        }
        else if (_lblVinculacion.ForeColor != Tema.Exito && _lblVinculacion.ForeColor != Tema.Error)
        {
            _lblVinculacion.ForeColor = Tema.TintaSuave;
            _lblVinculacion.Text = seleccionado == null ? "Elija un jugador haciendo clic en su tarjeta." : string.Empty;
        }
    }

    private void ActualizarCajero()
    {
        string texto;
        if (ModoSinHardware)
        {
            texto = "Modo sin hardware (pruebas)";
        }
        else if (PicoConectada)
        {
            texto = _sesion.EsOrganizador ? $"Conectada · {_sesion.Cajero.Descripcion}" : "Conectada al banco";
        }
        else
        {
            texto = "Desconectada";
        }

        _lblPico.ForeColor = ColorPico();
        _lblPico.ColorIndicador = ColorPico();
        _lblPico.Font = Tema.Texto(10.5f, FontStyle.Bold);
        _lblPico.Text = texto;
        _lblPico.Invalidate();
        _tarjetaPico.Invalidate();
        ActualizarVinculacion();
        ActualizarInicio();
    }

    private bool ModoSinHardware => _sesion.EsOrganizador ? _sesion.ModoSinHardware : _estado?.ModoSinHardware == true;

    private bool PicoConectada => _sesion.EsOrganizador ? PicoConectadaAqui : _estado?.CajeroConectado == true;

    private Color ColorPico()
    {
        return ModoSinHardware ? Tema.Informacion : PicoConectada ? Tema.Exito : Tema.Error;
    }

    private void DibujarDireccion(Graphics g)
    {
        Dibujo.Calidad(g);
        // La dirección principal, lo más grande que quepa en la tarjeta.
        float tamanio = 16f;
        Font grande = Tema.Monoespaciada(tamanio, FontStyle.Bold);
        while (tamanio > 9f && g.MeasureString(_direccionPrincipal, grande).Width > AnchoDerecha - 50)
        {
            grande.Dispose();
            tamanio -= 0.5f;
            grande = Tema.Monoespaciada(tamanio, FontStyle.Bold);
        }

        using (grande)
        using (SolidBrush tinta = new SolidBrush(Tema.Tinta))
        {
            g.DrawString(_direccionPrincipal, grande, tinta, 22, 74);
        }

        using Font pequenia = Tema.Texto(8.5f);
        using SolidBrush suave = new SolidBrush(Tema.TintaSuave);
        using StringFormat corte = new StringFormat { Trimming = StringTrimming.EllipsisCharacter };
        g.DrawString(_otrasDirecciones, pequenia, suave, new RectangleF(24, 110, AnchoDerecha - 172, 46), corte);
    }

    private void CopiarDireccion()
    {
        try
        {
            Clipboard.SetText(_direccionPrincipal);
            _notificaciones.Mostrar($"Dirección copiada: {_direccionPrincipal}", TipoNotificacion.Exito);
        }
        catch (ExternalException)
        {
            _notificaciones.Mostrar("No se pudo usar el portapapeles; copie la dirección a mano.", TipoNotificacion.Error);
        }
    }

    private void AlDesconectar(string motivo)
    {
        _btnIniciar.Enabled = false;
        bool puedeVolver = !_sesion.EsOrganizador && !_sesion.Cliente.CerradoPorElServidor;
        MessageBox.Show(this, motivo + (puedeVolver ? "\n\nPuede volver a unirse con el mismo nombre." : string.Empty),
            "Conexión perdida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        VolverParaReconectar = puedeVolver;
        Close();
    }

    private void AbrirOpciones()
    {
        if (_opciones == null || _opciones.IsDisposed)
        {
            _opciones = new FormularioOpciones(_sesion, SalirDeLaSala);
            _opciones.Show(this);
        }
        else
        {
            _opciones.Activate();
        }
    }

    private void SalirDeLaSala()
    {
        string aviso = _sesion.EsOrganizador
            ? "Usted aloja al banco: si sale, la partida se cerrará para todos. ¿Desea salir?"
            : "¿Desea salir de la sala de espera?";
        if (MessageBox.Show(this, aviso, "Salir de la partida", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            Close();
        }
    }

    private void AlPerderPico()
    {
        _notificaciones.Mostrar("Se perdió la conexión con la Pico W. Revise el cable y conéctela desde las opciones (engranaje).", TipoNotificacion.Error, 8000);
    }

    private static int? PrimeroSinTarjeta(EstadoRed estado)
    {
        foreach (EstadoJugador jugador in estado.Instantanea.Jugadores)
        {
            if (!jugador.TieneTarjetaFisica)
            {
                return jugador.Id;
            }
        }

        return null;
    }

    private static string Corto(string nombre)
    {
        return nombre.Length <= 10 ? nombre : nombre.Substring(0, 9) + "…";
    }

    /// <summary>
    /// Dirección principal para compartir (la primera IPv4 de la red local, o la del servidor al que se
    /// conectó) y el resto de direcciones del equipo, en texto pequeño.
    /// </summary>
    internal static (string Principal, string Otras) Direcciones(SesionJuego sesion)
    {
        if (!sesion.EsOrganizador)
        {
            return ($"{sesion.Host}:{sesion.Puerto}", "Dirección del banco del organizador.");
        }

        ListaSimple<string> ips = Servidor.ObtenerIPv4Locales();
        string principal = ips.Cantidad > 0 ? $"{ips.Obtener(0)}:{sesion.Puerto}" : $"127.0.0.1:{sesion.Puerto}";
        string otras = string.Empty;
        for (int i = 1; i < ips.Cantidad; i++)
        {
            otras += (otras.Length > 0 ? " · " : "También: ") + ips.Obtener(i);
        }

        otras += (otras.Length > 0 ? "\n" : string.Empty) + $"En esta computadora: 127.0.0.1:{sesion.Puerto}";
        return (principal, otras);
    }
}
