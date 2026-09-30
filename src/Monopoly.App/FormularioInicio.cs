using System;
using System.Drawing;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;
using Monopoly.Core.Logica;
using Monopoly.Core.Red;

namespace Monopoly.App;

/// <summary>
/// Ventana de inicio: nombre del jugador y dos opciones, crear la partida (el organizador aloja al
/// servidor en esta misma aplicación) o unirse a una existente por IP y puerto.
/// </summary>
internal sealed class FormularioInicio : Form
{
    /// <summary>
    /// Tiempo máximo para recibir BIENVENIDA o ERROR después de conectar.
    /// </summary>
    private const int TiempoEsperaBienvenidaMs = 8000;

    private readonly ArgumentosInicio _argumentos;
    private readonly TextBox _txtNombre = new TextBox();
    private readonly NumericUpDown _nudPuertoCrear = CrearNumero(1024, 65535, Servidor.PuertoPredeterminado);
    private readonly NumericUpDown _nudMaximoTurnos = CrearNumero(1, 2000, OpcionesJuego.MaximoTurnosPredeterminado);
    private readonly TextBox _txtIp = new TextBox { Text = "127.0.0.1" };
    private readonly NumericUpDown _nudPuertoUnirse = CrearNumero(1, 65535, Servidor.PuertoPredeterminado);
    private readonly Button _btnCrear = new Button { Text = "Crear partida" };
    private readonly Button _btnUnirse = new Button { Text = "Unirse a partida" };
    private readonly Label _lblEstado = new Label { AutoSize = false };
    private SesionJuego? _sesionPendiente;

    /// <summary>
    /// Crea la ventana de inicio.
    /// </summary>
    /// <param name="argumentos">Valores iniciales (por ejemplo, desde la línea de comandos).</param>
    public FormularioInicio(ArgumentosInicio argumentos)
    {
        _argumentos = argumentos;
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Monopoly Distribuido";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(520, 520);
        BackColor = Paleta.FondoPanel;
        Font = new Font(Paleta.Fuente, 10f);

        Panel cabecera = new Panel { Dock = DockStyle.Top, Height = 90, BackColor = Paleta.FondoTablero };
        cabecera.Paint += (s, e) =>
        {
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using Font titulo = new Font("Georgia", 22f, FontStyle.Bold);
            using Font subtitulo = new Font(Paleta.Fuente, 10f, FontStyle.Italic);
            using SolidBrush rojo = new SolidBrush(Paleta.RojoTitulo);
            e.Graphics.DrawString("MONOPOLY DISTRIBUIDO", titulo, rojo, 20, 14);
            e.Graphics.DrawString("Estructuras lineales · edición clásica en español", subtitulo, Brushes.DarkGreen, 24, 56);
            e.Graphics.DrawLine(Pens.Black, 0, cabecera.Height - 1, cabecera.Width, cabecera.Height - 1);
        };
        Controls.Add(cabecera);

        Controls.Add(new Label { Text = "Su nombre:", Location = new Point(24, 110), AutoSize = true });
        _txtNombre.SetBounds(130, 106, 360, 26);
        _txtNombre.MaxLength = 20;
        Controls.Add(_txtNombre);

        GroupBox grupoCrear = new GroupBox { Text = "Crear partida (organizador)", Location = new Point(24, 148), Size = new Size(466, 140) };
        grupoCrear.Controls.Add(new Label { Text = "Este equipo alojará al banco (servidor).", Location = new Point(16, 26), AutoSize = true, ForeColor = Color.DimGray });
        grupoCrear.Controls.Add(new Label { Text = "Puerto:", Location = new Point(16, 60), AutoSize = true });
        _nudPuertoCrear.SetBounds(90, 56, 90, 26);
        grupoCrear.Controls.Add(_nudPuertoCrear);
        grupoCrear.Controls.Add(new Label { Text = "Máximo de turnos:", Location = new Point(200, 60), AutoSize = true });
        _nudMaximoTurnos.SetBounds(340, 56, 100, 26);
        grupoCrear.Controls.Add(_nudMaximoTurnos);
        _btnCrear.SetBounds(16, 94, 424, 34);
        _btnCrear.Click += async (s, e) => await CrearPartidaAsync();
        grupoCrear.Controls.Add(_btnCrear);
        Controls.Add(grupoCrear);

        GroupBox grupoUnirse = new GroupBox { Text = "Unirse a partida", Location = new Point(24, 300), Size = new Size(466, 110) };
        grupoUnirse.Controls.Add(new Label { Text = "IP:", Location = new Point(16, 32), AutoSize = true });
        _txtIp.SetBounds(50, 28, 200, 26);
        grupoUnirse.Controls.Add(_txtIp);
        grupoUnirse.Controls.Add(new Label { Text = "Puerto:", Location = new Point(270, 32), AutoSize = true });
        _nudPuertoUnirse.SetBounds(340, 28, 100, 26);
        grupoUnirse.Controls.Add(_nudPuertoUnirse);
        _btnUnirse.SetBounds(16, 64, 424, 34);
        _btnUnirse.Click += async (s, e) => await UnirseAsync();
        grupoUnirse.Controls.Add(_btnUnirse);
        Controls.Add(grupoUnirse);

        _lblEstado.SetBounds(24, 418, 466, 90);
        Controls.Add(_lblEstado);

        AcceptButton = _btnUnirse;
        AplicarArgumentos();
    }

    /// <summary>
    /// El servidor aceptó al jugador: la sesión queda en manos de quien escucha este evento.
    /// </summary>
    public event Action<SesionJuego>? SesionCreada;

    /// <inheritdoc/>
    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_argumentos.Modo == ModoInicio.Crear)
        {
            await CrearPartidaAsync();
        }
        else if (_argumentos.Modo == ModoInicio.Unirse)
        {
            await UnirseAsync();
        }
    }

    /// <inheritdoc/>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        // Si se cierra sin haber entrado a la partida, se libera la conexión pendiente.
        _sesionPendiente?.Dispose();
        base.OnFormClosed(e);
    }

    private async Task CrearPartidaAsync()
    {
        string? nombre = ValidarNombre();
        if (nombre == null)
        {
            return;
        }

        int puerto = (int)_nudPuertoCrear.Value;
        Servidor servidor;
        try
        {
            Juego juego = new Juego(new OpcionesJuego { MaximoTurnos = (int)_nudMaximoTurnos.Value });
            servidor = new Servidor(juego, puerto);
            servidor.Iniciar();
        }
        catch (SocketException ex)
        {
            string detalle = ex.SocketErrorCode == SocketError.AddressAlreadyInUse
                ? $"El puerto {puerto} ya está en uso en esta computadora (¿hay otra partida abierta?). Cierre la otra partida o elija otro puerto."
                : $"No se pudo abrir el puerto {puerto}: {ex.Message}";
            MostrarError(detalle, "No se pudo crear la partida");
            return;
        }

        // El organizador se conecta a su propio servidor como un cliente más.
        await ConectarAsync("127.0.0.1", puerto, nombre, servidor);
    }

    private async Task UnirseAsync()
    {
        string? nombre = ValidarNombre();
        string ip = _txtIp.Text.Trim();
        if (nombre == null)
        {
            return;
        }

        if (ip.Length == 0)
        {
            MostrarError("Escriba la IP de la computadora del organizador (se muestra en su sala de espera).");
            _txtIp.Focus();
            return;
        }

        if (!EsDireccionValida(ip))
        {
            MostrarError($"'{ip}' no es una IP válida. Debe tener la forma 192.168.1.20 (cuatro números separados por puntos).", "IP incorrecta");
            _txtIp.Focus();
            return;
        }

        await ConectarAsync(ip, (int)_nudPuertoUnirse.Value, nombre, null);
    }

    /// <summary>
    /// Acepta una IPv4 completa (cuatro números de 0 a 255) o un nombre de equipo.
    /// </summary>
    private static bool EsDireccionValida(string direccion)
    {
        bool pareceIp = true;
        foreach (char caracter in direccion)
        {
            if (!char.IsDigit(caracter) && caracter != '.')
            {
                pareceIp = false;
                break;
            }
        }

        if (!pareceIp)
        {
            return Uri.CheckHostName(direccion) == UriHostNameType.Dns;
        }

        string[] partes = direccion.Split('.');
        if (partes.Length != 4)
        {
            return false;
        }

        foreach (string parte in partes)
        {
            if (parte.Length == 0 || parte.Length > 3 || int.Parse(parte, System.Globalization.CultureInfo.InvariantCulture) > 255)
            {
                return false;
            }
        }

        return true;
    }

    private async Task ConectarAsync(string host, int puerto, string nombre, Servidor? servidor)
    {
        HabilitarBotones(false);
        MostrarInformacion($"Conectando a {host}:{puerto}...");

        SesionJuego sesion = new SesionJuego(new Cliente(), servidor, host, puerto);
        _sesionPendiente = sesion;
        Action<int, string>? alBienvenida = null;
        Action<string>? alError = null;
        Timer esperaRespuesta = new Timer { Interval = TiempoEsperaBienvenidaMs };

        void Terminar()
        {
            esperaRespuesta.Stop();
            esperaRespuesta.Dispose();
            sesion.BienvenidaRecibida -= alBienvenida;
            sesion.ErrorRecibido -= alError;
            sesion.Desconectado -= alError;
            _sesionPendiente = null;
        }

        void Fallar(string mensaje)
        {
            Terminar();
            sesion.Dispose();
            MostrarError(mensaje, "No se pudo unir a la partida");
            HabilitarBotones(true);
        }

        alBienvenida = (id, nombreConfirmado) =>
        {
            Terminar();
            SesionCreada?.Invoke(sesion);
        };

        // Rechazos del servidor: partida llena, ya iniciada, nombre repetido, etc. (el texto viene del banco).
        alError = mensaje => Fallar(mensaje);
        sesion.BienvenidaRecibida += alBienvenida;
        sesion.ErrorRecibido += alError;
        sesion.Desconectado += alError;
        esperaRespuesta.Tick += (s, e) =>
            Fallar($"Se abrió la conexión con {host}:{puerto}, pero no respondió como una partida de Monopoly. ¿Es correcto el puerto?");

        try
        {
            await Task.Run(() => sesion.Cliente.Conectar(host, puerto));
        }
        catch (Exception ex) when (ex is SocketException || ex is TimeoutException || ex is ArgumentException)
        {
            Fallar(Cliente.DescribirErrorConexion(ex, host, puerto));
            return;
        }

        esperaRespuesta.Start();
        sesion.Solicitar(cliente => cliente.Unirse(nombre));
    }

    private string? ValidarNombre()
    {
        string nombre = _txtNombre.Text.Trim();
        if (nombre.Length == 0)
        {
            MostrarError("Escriba su nombre.");
            _txtNombre.Focus();
            return null;
        }

        if (nombre.IndexOf('|') >= 0)
        {
            MostrarError("El nombre no puede contener el carácter |.");
            return null;
        }

        return nombre;
    }

    private void AplicarArgumentos()
    {
        _txtNombre.Text = _argumentos.Nombre ?? string.Empty;
        if (_argumentos.Host != null)
        {
            _txtIp.Text = _argumentos.Host;
        }

        if (_argumentos.Puerto.HasValue)
        {
            _nudPuertoCrear.Value = _argumentos.Puerto.Value;
            _nudPuertoUnirse.Value = _argumentos.Puerto.Value;
        }

        if (_argumentos.MaximoTurnos.HasValue)
        {
            _nudMaximoTurnos.Value = Math.Min(Math.Max(_argumentos.MaximoTurnos.Value, 1), 2000);
        }
    }

    private void HabilitarBotones(bool habilitar)
    {
        _btnCrear.Enabled = habilitar;
        _btnUnirse.Enabled = habilitar;
    }

    /// <summary>
    /// Muestra un error en la etiqueta y, si se indica un título, también en un cuadro de diálogo.
    /// </summary>
    private void MostrarError(string mensaje, string? titulo = null)
    {
        _lblEstado.ForeColor = Color.FromArgb(170, 20, 20);
        _lblEstado.Text = mensaje;
        if (titulo != null && Visible)
        {
            MessageBox.Show(this, mensaje, titulo, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void MostrarInformacion(string mensaje)
    {
        _lblEstado.ForeColor = Color.DimGray;
        _lblEstado.Text = mensaje;
    }

    private static NumericUpDown CrearNumero(int minimo, int maximo, int valor)
    {
        return new NumericUpDown { Minimum = minimo, Maximum = maximo, Value = valor };
    }
}
