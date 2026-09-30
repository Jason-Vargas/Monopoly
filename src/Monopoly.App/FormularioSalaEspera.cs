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
        ClientSize = new Size(600, 600);
        BackColor = Paleta.FondoPanel;
        Font = new Font(Paleta.Fuente, 10f);

        int y = 16;
        if (sesion.EsOrganizador)
        {
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
        _lstJugadores.SetBounds(20, y + 24, 560, 128);
        _lstJugadores.Font = new Font(Paleta.Fuente, 10.5f);
        Controls.Add(_lstJugadores);

        _lblEstado.SetBounds(20, y + 158, 560, 44);
        Controls.Add(_lblEstado);

        _btnIniciar.SetBounds(20, y + 206, 560, 42);
        _btnIniciar.Visible = sesion.EsOrganizador;
        _btnIniciar.Enabled = false;
        _btnIniciar.BackColor = Color.FromArgb(40, 140, 70);
        _btnIniciar.ForeColor = Color.White;
        _btnIniciar.FlatStyle = FlatStyle.Flat;
        _btnIniciar.Click += (s, e) => _sesion.Solicitar(cliente => cliente.IniciarPartida());
        Controls.Add(_btnIniciar);

        int yEventos = sesion.EsOrganizador ? y + 258 : y + 206;
        _lstEventos.SetBounds(20, yEventos, 560, ClientSize.Height - yEventos - 16);
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

        _lstJugadores.BeginUpdate();
        _lstJugadores.Items.Clear();
        for (int i = 0; i < instantanea.Jugadores.Length; i++)
        {
            EstadoJugador j = instantanea.Jugadores[i];
            string marcas = (i == 0 ? " · organizador" : string.Empty) + (j.Id == _sesion.IdJugador ? " · usted" : string.Empty)
                + (estado.EstaConectado(j.Id) ? string.Empty : " · DESCONECTADO");
            _lstJugadores.Items.Add($"{j.Id}. {j.Nombre} — ficha {j.ColorFicha}{marcas}");
        }

        _lstJugadores.EndUpdate();

        int cantidad = instantanea.Jugadores.Length;
        bool puedeIniciar = cantidad >= Juego.MinimoJugadores && cantidad <= Juego.MaximoJugadores;
        _btnIniciar.Enabled = _sesion.EsOrganizador && puedeIniciar;
        _lblEstado.ForeColor = Color.Black;
        string organizador = cantidad > 0 ? instantanea.Jugadores[0].Nombre : "el organizador";
        _lblEstado.Text = _sesion.EsOrganizador
            ? (puedeIniciar
                ? $"Jugadores: {cantidad}/{Juego.MaximoJugadores}. Puede iniciar la partida cuando estén todos."
                : $"Jugadores: {cantidad}/{Juego.MaximoJugadores}. Se necesitan al menos {Juego.MinimoJugadores} para iniciar.")
            : $"Jugadores: {cantidad}/{Juego.MaximoJugadores}. Esperando a que {organizador} inicie la partida...";
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
