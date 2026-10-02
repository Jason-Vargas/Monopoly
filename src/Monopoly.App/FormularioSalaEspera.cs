using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Monopoly.Core.Logica;
using Monopoly.Core.Red;

namespace Monopoly.App;

/// <summary>
/// Sala de espera: muestra los jugadores conectados y, al organizador, la dirección para compartir.
/// Solo el organizador puede iniciar la partida, cuando hay entre 2 y 4 jugadores.
/// </summary>
internal sealed class FormularioSalaEspera : Form
{
    private readonly SesionJuego _sesion;
    private readonly ListBox _lstJugadores = new ListBox { IntegralHeight = false };
    private readonly ListBox _lstEventos = new ListBox { IntegralHeight = false, HorizontalScrollbar = true };
    private readonly Label _lblEstado = new Label { AutoSize = false };
    private readonly Button _btnIniciar = new Button { Text = "Iniciar partida" };
    private readonly Button _btnVincular = new Button { Text = "Vincular tarjeta" };
    private readonly Button _btnCancelarVinculacion = new Button { Text = "Cancelar vinculación" };
    private readonly Label _lblVinculacion = new Label { AutoSize = false, ForeColor = Color.DimGray };
    private BarraCajero? _barraCajero;
    private EstadoJugador[] _jugadoresMostrados = new EstadoJugador[0];
    private int? _esperandoTarjetaDe;
    private bool _partidaIniciada;

    /// <summary>
    /// Crea la sala de espera de una sesión ya aceptada por el servidor.
    /// </summary>
    public FormularioSalaEspera(SesionJuego sesion)
    {
        _sesion = sesion;
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = $"Sala de espera — {sesion.Nombre}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        int ancho = sesion.EsOrganizador ? 780 : 560;
        ClientSize = new Size(ancho + 40, sesion.EsOrganizador ? 640 : 600);
        BackColor = Paleta.FondoPanel;
        Font = new Font(Paleta.Fuente, 10f);

        int y = 16;
        if (sesion.EsOrganizador)
        {
            // El organizador aloja al banco y, por lo tanto, al cajero (Pico W por USB).
            _barraCajero = new BarraCajero(sesion) { Dock = DockStyle.Top };
            Controls.Add(_barraCajero);
            y += 34;

            Label aviso = new Label { Text = "Usted aloja al banco. Comparta esta dirección con los demás:", AutoSize = false };
            aviso.SetBounds(20, y, 560, 42);
            Controls.Add(aviso);
            TextBox direcciones = new TextBox
            {
                ReadOnly = true,
                Multiline = true,
                TabStop = false,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 11f, FontStyle.Bold),
                BackColor = Color.White,
                Text = TextoDirecciones(sesion.Puerto),
            };
            direcciones.SetBounds(20, y + 44, 560, 76);
            Controls.Add(direcciones);
            y += 134;
        }
        else
        {
            Controls.Add(new Label { Text = $"Conectado al banco en {sesion.Host}:{sesion.Puerto}.", Location = new Point(20, y), AutoSize = true });
            y += 36;
        }

        Controls.Add(new Label { Text = "Jugadores conectados:", Location = new Point(20, y), AutoSize = true, Font = new Font(Paleta.Fuente, 10f, FontStyle.Bold) });
        _lstJugadores.SetBounds(20, y + 24, sesion.EsOrganizador ? ancho - 220 : ancho, 128);
        _lstJugadores.Font = new Font(Paleta.Fuente, 10.5f);
        Controls.Add(_lstJugadores);

        if (sesion.EsOrganizador)
        {
            // Registro de tarjetas: el organizador elige un jugador y la próxima tarjeta leída queda vinculada.
            int x = 20 + ancho - 205;
            _btnVincular.SetBounds(x, y + 24, 205, 36);
            _btnVincular.Click += (s, e) => VincularTarjetaSeleccionado();
            _btnCancelarVinculacion.SetBounds(x, y + 64, 205, 30);
            _btnCancelarVinculacion.Click += (s, e) => CancelarVinculacion();
            _lblVinculacion.SetBounds(x, y + 98, 205, 56);
            _lblVinculacion.Font = new Font(Paleta.Fuente, 8.5f);
            Controls.Add(_btnVincular);
            Controls.Add(_btnCancelarVinculacion);
            Controls.Add(_lblVinculacion);
            _lstJugadores.SelectedIndexChanged += (s, e) => ActualizarVinculacion();
            _sesion.CajeroCambiado += ActualizarVinculacion;
        }

        _lblEstado.SetBounds(20, y + 158, ancho, 44);
        Controls.Add(_lblEstado);

        _btnIniciar.SetBounds(20, y + 206, ancho, 42);
        _btnIniciar.Visible = sesion.EsOrganizador;
        _btnIniciar.Enabled = false;
        _btnIniciar.BackColor = Color.FromArgb(40, 140, 70);
        _btnIniciar.ForeColor = Color.White;
        _btnIniciar.FlatStyle = FlatStyle.Flat;
        _btnIniciar.Click += (s, e) => _sesion.Solicitar(cliente => cliente.IniciarPartida());
        Controls.Add(_btnIniciar);

        int yEventos = sesion.EsOrganizador ? y + 258 : y + 206;
        _lstEventos.SetBounds(20, yEventos, ancho, ClientSize.Height - yEventos - 16);
        _lstEventos.Font = new Font(Paleta.Fuente, 9f);
        _lstEventos.ForeColor = Color.DimGray;
        Controls.Add(_lstEventos);

        _sesion.EstadoActualizado += AlActualizarEstado;
        _sesion.EventoRecibido += AlRecibirEvento;
        _sesion.ErrorRecibido += AlRecibirError;
        _sesion.Desconectado += AlDesconectar;
        _sesion.RecorrerEventos(texto => _lstEventos.Items.Add(texto));
    }

    /// <summary>
    /// La partida comenzó: hay que pasar a la ventana de juego.
    /// </summary>
    public event Action? PartidaIniciada;

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
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _sesion.EstadoActualizado -= AlActualizarEstado;
        _sesion.EventoRecibido -= AlRecibirEvento;
        _sesion.ErrorRecibido -= AlRecibirError;
        _sesion.Desconectado -= AlDesconectar;
        _sesion.CajeroCambiado -= ActualizarVinculacion;
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

        int seleccionado = _lstJugadores.SelectedIndex;
        _jugadoresMostrados = instantanea.Jugadores;
        _lstJugadores.BeginUpdate();
        _lstJugadores.Items.Clear();
        for (int i = 0; i < instantanea.Jugadores.Length; i++)
        {
            EstadoJugador j = instantanea.Jugadores[i];
            string marcas = (i == 0 ? " · organizador" : string.Empty) + (j.Id == _sesion.IdJugador ? " · usted" : string.Empty)
                + (j.TieneTarjetaFisica ? " · tarjeta RFID" : string.Empty)
                + (estado.EstaConectado(j.Id) ? string.Empty : " · DESCONECTADO");
            _lstJugadores.Items.Add($"{j.Id}. {j.Nombre} — ficha {j.ColorFicha}{marcas}");
        }

        if (seleccionado >= 0 && seleccionado < _lstJugadores.Items.Count)
        {
            _lstJugadores.SelectedIndex = seleccionado;
        }

        _lstJugadores.EndUpdate();

        // Si llegó la tarjeta del jugador que se estaba vinculando, termina la espera.
        if (_esperandoTarjetaDe.HasValue && estado.BuscarJugador(_esperandoTarjetaDe.Value)?.TieneTarjetaFisica == true)
        {
            string nombre = estado.BuscarJugador(_esperandoTarjetaDe.Value)!.Nombre;
            _esperandoTarjetaDe = null;
            _lblVinculacion.ForeColor = Color.FromArgb(20, 110, 40);
            _lblVinculacion.Text = $"Tarjeta vinculada a {nombre}.";
        }

        ActualizarVinculacion();

        int cantidad = instantanea.Jugadores.Length;
        bool cantidadValida = cantidad >= Juego.MinimoJugadores && cantidad <= Juego.MaximoJugadores;

        // En modo hardware todos necesitan una tarjeta física vinculada (el servidor también lo exige).
        string sinTarjeta = string.Empty;
        if (!estado.ModoSinHardware)
        {
            foreach (EstadoJugador j in instantanea.Jugadores)
            {
                if (!j.TieneTarjetaFisica)
                {
                    sinTarjeta += (sinTarjeta.Length > 0 ? ", " : string.Empty) + j.Nombre;
                }
            }
        }

        bool puedeIniciar = cantidadValida && sinTarjeta.Length == 0;
        _btnIniciar.Enabled = _sesion.EsOrganizador && puedeIniciar;
        _lblEstado.ForeColor = Color.Black;
        string organizador = cantidad > 0 ? instantanea.Jugadores[0].Nombre : "el organizador";
        string texto = $"Jugadores: {cantidad}/{Juego.MaximoJugadores}. ";
        if (!_sesion.EsOrganizador)
        {
            texto += $"Esperando a que {organizador} inicie la partida...";
        }
        else if (!cantidadValida)
        {
            texto += $"Se necesitan al menos {Juego.MinimoJugadores} para iniciar.";
        }
        else if (sinTarjeta.Length > 0)
        {
            _lblEstado.ForeColor = Color.FromArgb(200, 100, 0);
            texto += $"Vincule una tarjeta a: {sinTarjeta} (o active el modo sin hardware para pruebas).";
        }
        else
        {
            texto += "Puede iniciar la partida cuando estén todos.";
        }

        _lblEstado.Text = texto;
    }

    private void AlRecibirEvento(string texto)
    {
        _lstEventos.Items.Add(texto);
        _lstEventos.TopIndex = Math.Max(0, _lstEventos.Items.Count - 1);
    }

    private void AlRecibirError(string mensaje)
    {
        _lblEstado.ForeColor = Color.FromArgb(170, 20, 20);
        _lblEstado.Text = mensaje;
        if (_esperandoTarjetaDe.HasValue)
        {
            // Por ejemplo, "La tarjeta X ya está vinculada a Beto": la espera terminó sin vincular.
            _esperandoTarjetaDe = null;
            _lblVinculacion.ForeColor = Color.FromArgb(170, 20, 20);
            _lblVinculacion.Text = mensaje;
            ActualizarVinculacion();
        }
    }

    private void VincularTarjetaSeleccionado()
    {
        int indice = _lstJugadores.SelectedIndex;
        if (indice < 0 || indice >= _jugadoresMostrados.Length)
        {
            return;
        }

        EstadoJugador jugador = _jugadoresMostrados[indice];
        _esperandoTarjetaDe = jugador.Id;
        _lblVinculacion.ForeColor = Color.FromArgb(40, 90, 170);
        _lblVinculacion.Text = $"Acerque al lector la tarjeta de {jugador.Nombre}...";
        _sesion.Solicitar(cliente => cliente.VincularTarjeta(jugador.Id));
        ActualizarVinculacion();
    }

    private void CancelarVinculacion()
    {
        _esperandoTarjetaDe = null;
        _lblVinculacion.Text = string.Empty;
        _sesion.Solicitar(cliente => cliente.VincularTarjeta(0));
        ActualizarVinculacion();
    }

    private void ActualizarVinculacion()
    {
        if (!_sesion.EsOrganizador)
        {
            return;
        }

        bool picoConectada = _sesion.Cajero.EsFisico && _sesion.Cajero.Estado == Core.Hardware.EstadoCajero.Conectado;
        if (!picoConectada && _esperandoTarjetaDe.HasValue)
        {
            _esperandoTarjetaDe = null;
            _lblVinculacion.Text = string.Empty;
        }

        _btnVincular.Enabled = picoConectada && _lstJugadores.SelectedIndex >= 0;
        _btnCancelarVinculacion.Enabled = _esperandoTarjetaDe.HasValue;
        if (!picoConectada && _lblVinculacion.Text.Length == 0)
        {
            _lblVinculacion.ForeColor = Color.DimGray;
            _lblVinculacion.Text = "Conecte la Pico W (arriba) para vincular tarjetas.";
        }
        else if (picoConectada && !_esperandoTarjetaDe.HasValue && _lblVinculacion.ForeColor == Color.DimGray)
        {
            _lblVinculacion.Text = "Elija un jugador y pulse \"Vincular tarjeta\".";
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

    /// <summary>
    /// Indica si la ventana se cerró para volver al inicio y reconectarse.
    /// </summary>
    public bool VolverParaReconectar { get; private set; }

    private static string TextoDirecciones(int puerto)
    {
        StringBuilder texto = new StringBuilder();
        Servidor.ObtenerIPv4Locales().Recorrer(ip => texto.Append(ip).Append(':').Append(puerto).Append("\r\n"));
        texto.Append("127.0.0.1:").Append(puerto).Append("  (esta computadora)");
        return texto.ToString();
    }
}
