using System;
using System.Drawing;
using System.Windows.Forms;
using Monopoly.Core.Logica;
using Monopoly.Core.Modelo;
using Monopoly.Core.Red;

namespace Monopoly.App;

/// <summary>
/// Ventana principal de la partida: tablero, jugadores, botones de acción, registro de eventos y
/// acceso al historial. Todo lo que muestra proviene de los mensajes del servidor; los botones solo
/// envían solicitudes y se habilitan según el último estado recibido (el servidor valida igualmente).
/// </summary>
internal sealed class FormularioJuego : Form
{
    private const int MaximoLineasRegistro = 600;

    private readonly SesionJuego _sesion;
    private readonly Tablero _tablero = new Tablero();
    private readonly PanelTablero _panelTablero = new PanelTablero { Dock = DockStyle.Fill };
    private readonly PanelJugadores _panelJugadores = new PanelJugadores { Dock = DockStyle.Fill };
    private readonly Label _lblInfo = new Label { Dock = DockStyle.Fill, AutoSize = false, Padding = new Padding(6, 2, 6, 2), TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _btnTirar = CrearBoton("Tirar dados", Color.FromArgb(40, 120, 200));
    private readonly Button _btnComprar = CrearBoton("Comprar", Color.FromArgb(40, 140, 70));
    private readonly Button _btnNoComprar = CrearBoton("No comprar", Color.FromArgb(120, 120, 120));
    private readonly Button _btnPagar = CrearBoton("Pagar con tarjeta", Color.FromArgb(200, 120, 20));
    private readonly Button _btnTerminar = CrearBoton("Terminar turno", Color.FromArgb(150, 40, 40));
    private readonly Button _btnHistorial = CrearBoton("Historial de transacciones...", Color.FromArgb(70, 70, 90));
    private readonly ListBox _lstRegistro = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true };
    private readonly ToolStripStatusLabel _lblMensaje = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel _lblConexion = new ToolStripStatusLabel();
    private FormularioHistorial? _historial;
    private bool _animarProximoEstado;
    private bool _finMostrado;
    private bool _desconectado;

    /// <summary>
    /// Crea la ventana de juego para una sesión.
    /// </summary>
    public FormularioJuego(SesionJuego sesion)
    {
        _sesion = sesion;
        Text = $"Monopoly Distribuido — {sesion.Nombre} (jugador {sesion.IdJugador})";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1280, 860);
        MinimumSize = new Size(1000, 820);
        BackColor = Paleta.FondoPanel;
        Font = new Font(Paleta.Fuente, 9.5f);

        Controls.Add(CrearDistribucion());
        StatusStrip barra = new StatusStrip();
        _lblConexion.Text = sesion.EsOrganizador
            ? $"Banco alojado en este equipo · puerto {sesion.Puerto}"
            : $"Conectado a {sesion.Host}:{sesion.Puerto}";
        barra.Items.Add(_lblMensaje);
        barra.Items.Add(_lblConexion);
        Controls.Add(barra);

        _btnTirar.Click += (s, e) => Solicitar(cliente => cliente.TirarDados());
        _btnComprar.Click += (s, e) => Solicitar(cliente => cliente.ComprarPropiedad());
        _btnNoComprar.Click += (s, e) => Solicitar(cliente => cliente.NoComprar());
        _btnPagar.Click += (s, e) => Solicitar(cliente => cliente.PagarConTarjeta());
        _btnTerminar.Click += (s, e) => Solicitar(cliente => cliente.TerminarTurno());
        _btnHistorial.Click += (s, e) => AbrirHistorial();

        _sesion.EstadoActualizado += AlActualizarEstado;
        _sesion.EventoRecibido += AlRecibirEvento;
        _sesion.DadosRecibidos += AlRecibirDados;
        _sesion.ErrorRecibido += AlRecibirError;
        _sesion.FinRecibido += AlRecibirFin;
        _sesion.Desconectado += AlDesconectar;

        _sesion.RecorrerEventos(AgregarAlRegistro);
        HabilitarAcciones(null);
        if (_sesion.UltimoEstado != null)
        {
            AlActualizarEstado(_sesion.UltimoEstado);
        }
    }

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
        _historial?.Close();
        base.OnFormClosed(e);
    }

    private Control CrearDistribucion()
    {
        TableLayoutPanel principal = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        principal.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        principal.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 400f));
        principal.Controls.Add(_panelTablero, 0, 0);

        TableLayoutPanel lateral = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(4) };
        lateral.RowStyles.Add(new RowStyle(SizeType.Absolute, PanelJugadores.AltoPreferido));
        lateral.RowStyles.Add(new RowStyle(SizeType.Absolute, 86f));
        lateral.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        lateral.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        lateral.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        lateral.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        lateral.Controls.Add(_panelJugadores, 0, 0);
        _lblInfo.Font = new Font(Paleta.Fuente, 10f, FontStyle.Bold);
        _lblInfo.BorderStyle = BorderStyle.FixedSingle;
        lateral.Controls.Add(_lblInfo, 0, 1);

        FlowLayoutPanel botones = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 4, 0, 4) };
        _btnTirar.Width = 382;
        botones.Controls.Add(_btnTirar);
        botones.Controls.Add(_btnComprar);
        botones.Controls.Add(_btnNoComprar);
        botones.Controls.Add(_btnPagar);
        botones.Controls.Add(_btnTerminar);
        lateral.Controls.Add(botones, 0, 2);

        lateral.Controls.Add(new Label { Text = "Registro de la partida", AutoSize = true, Font = new Font(Paleta.Fuente, 9.5f, FontStyle.Bold), Margin = new Padding(3, 6, 3, 2) }, 0, 3);
        _lstRegistro.Font = new Font(Paleta.Fuente, 9f);
        lateral.Controls.Add(_lstRegistro, 0, 4);
        _btnHistorial.Width = 382;
        lateral.Controls.Add(_btnHistorial, 0, 5);

        principal.Controls.Add(lateral, 1, 0);
        return principal;
    }

    private void AlActualizarEstado(EstadoRed estado)
    {
        _panelTablero.MostrarEstado(estado, _animarProximoEstado);
        _animarProximoEstado = false;
        _panelJugadores.MostrarEstado(estado, _sesion.IdJugador);
        HabilitarAcciones(estado);
        _lblInfo.Text = DescribirSituacion(estado);
    }

    private void AlRecibirDados(int idJugador, int dado1, int dado2)
    {
        // El ESTADO que sigue a los dados trae el recorrido: se anima casilla por casilla.
        _animarProximoEstado = true;
    }

    private void AlRecibirEvento(string texto)
    {
        AgregarAlRegistro(texto);
    }

    private void AlRecibirError(string mensaje)
    {
        _lblMensaje.ForeColor = Color.FromArgb(170, 20, 20);
        _lblMensaje.Text = "⚠ " + mensaje;
        AgregarAlRegistro("⚠ " + mensaje);
    }

    private void AlRecibirFin(string ganador, string resumen)
    {
        HabilitarAcciones(_sesion.UltimoEstado);
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
        _lblConexion.Text = "Desconectado";
        _lblMensaje.ForeColor = Color.FromArgb(170, 20, 20);
        _lblMensaje.Text = motivo;
        if (_sesion.UltimoEstado?.Instantanea.Estado != EstadoPartida.Finalizada)
        {
            MessageBox.Show(this, motivo + "\nCierre la ventana y vuelva a entrar con el mismo nombre para continuar.",
                "Conexión perdida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void Solicitar(Action<Cliente> solicitud)
    {
        _lblMensaje.Text = string.Empty;
        _sesion.Solicitar(solicitud);
    }

    private void HabilitarAcciones(EstadoRed? estado)
    {
        InstantaneaJuego? i = estado?.Instantanea;
        int? yo = _sesion.IdJugador;
        bool enCurso = !_desconectado && i?.Estado == EstadoPartida.EnCurso;
        bool activo = yo.HasValue && (estado?.BuscarJugador(yo.Value)?.Activo ?? false);
        bool miTurno = enCurso && activo && i!.IdJugadorEnTurno == yo;

        _btnTirar.Enabled = miTurno && i!.Fase == FaseTurno.EsperandoDados;
        _btnComprar.Enabled = miTurno && i!.Fase == FaseTurno.EsperandoDecisionCompra;
        _btnNoComprar.Enabled = _btnComprar.Enabled;
        _btnPagar.Enabled = enCurso && yo.HasValue && i!.IdDeudor == yo;
        _btnTerminar.Enabled = miTurno && i!.Fase == FaseTurno.PuedeTerminar;
        _btnHistorial.Enabled = !_desconectado;

        _lblInfo.BackColor = miTurno || _btnPagar.Enabled ? Color.FromArgb(255, 238, 170) : Color.White;
    }

    private string DescribirSituacion(EstadoRed estado)
    {
        InstantaneaJuego i = estado.Instantanea;
        if (i.Estado == EstadoPartida.Finalizada)
        {
            return $"Partida finalizada. Ganador: {estado.BuscarJugador(i.IdGanador ?? 0)?.Nombre}.";
        }

        if (i.Estado != EstadoPartida.EnCurso || !i.IdJugadorEnTurno.HasValue)
        {
            return "Esperando el inicio de la partida.";
        }

        bool miTurno = i.IdJugadorEnTurno == _sesion.IdJugador;
        string quien = miTurno ? "Usted" : estado.BuscarJugador(i.IdJugadorEnTurno.Value)?.Nombre ?? "?";
        string accion;
        switch (i.Fase)
        {
            case FaseTurno.EsperandoDados:
                accion = $"{quien} debe lanzar los dados.";
                break;
            case FaseTurno.EsperandoDecisionCompra:
                Propiedad? propiedad = i.IdPropiedadEnVenta.HasValue ? _tablero.BuscarPropiedad(i.IdPropiedadEnVenta.Value) : null;
                accion = propiedad == null
                    ? $"{quien} decide si compra."
                    : $"{quien} decide si compra {propiedad.Nombre} por {Formato.Dinero(propiedad.PrecioCompra)}.";
                break;
            case FaseTurno.EsperandoPago:
                bool debo = i.IdDeudor == _sesion.IdJugador;
                accion = $"{i.DescripcionPagoPendiente}. " + (debo ? "Pase su tarjeta (Pagar con tarjeta)." : "Esperando la tarjeta.");
                break;
            default:
                accion = $"{quien} puede terminar el turno.";
                break;
        }

        return $"Turno {i.NumeroTurno}/{i.MaximoTurnos}{(miTurno ? " · ¡ES SU TURNO!" : string.Empty)}\n{accion}";
    }

    private void AgregarAlRegistro(string texto)
    {
        _lstRegistro.Items.Add(texto);
        if (_lstRegistro.Items.Count > MaximoLineasRegistro)
        {
            _lstRegistro.Items.RemoveAt(0);
        }

        _lstRegistro.TopIndex = Math.Max(0, _lstRegistro.Items.Count - 1);
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

    private static Button CrearBoton(string texto, Color color)
    {
        Button boton = new Button
        {
            Text = texto,
            Width = 188,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = color,
            ForeColor = Color.White,
            Font = new Font(Paleta.Fuente, 9.5f, FontStyle.Bold),
            Margin = new Padding(3),
        };
        boton.FlatAppearance.BorderColor = Color.FromArgb(40, 40, 40);
        boton.EnabledChanged += (s, e) => boton.BackColor = boton.Enabled ? color : Color.FromArgb(200, 200, 195);
        return boton;
    }
}
