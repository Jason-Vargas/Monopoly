using System;
using System.Drawing;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;
using Monopoly.App.Estilo;
using Monopoly.Core.Logica;
using Monopoly.Core.Red;

namespace Monopoly.App;

/// <summary>
/// Ventana de inicio: logotipo animado sobre un fondo de tablero, el jugador (nombre, ficha y color) y dos
/// tarjetas: crear la partida (el organizador aloja al servidor en esta misma aplicación) o unirse a una
/// existente por IP y puerto. Los campos se validan visualmente y los errores se muestran como
/// notificaciones.
/// </summary>
internal sealed class FormularioInicio : Form
{
    /// <summary>
    /// Tiempo máximo para recibir BIENVENIDA o ERROR después de conectar.
    /// </summary>
    private const int TiempoEsperaBienvenidaMs = 8000;

    private const int DuracionEntradaMs = 1300;
    private const int AltoCabecera = 176;

    private readonly ArgumentosInicio _argumentos;
    private readonly CampoTexto _txtNombre = new CampoTexto { Etiqueta = "Su nombre", Marcador = "Por ejemplo, Ana", LongitudMaxima = 20 };
    private readonly SelectorFicha _selectorFicha = new SelectorFicha();
    private readonly CampoTexto _txtMaximoTurnos = new CampoTexto { Etiqueta = "Máximo de turnos", SoloNumeros = true, LongitudMaxima = 4 };
    private readonly CampoTexto _txtPuertoCrear = new CampoTexto { Etiqueta = "Puerto", SoloNumeros = true, LongitudMaxima = 5 };
    private readonly CampoTexto _txtIp = new CampoTexto { Etiqueta = "IP del organizador", Marcador = "192.168.1.20", LongitudMaxima = 64 };
    private readonly CampoTexto _txtPuertoUnirse = new CampoTexto { Etiqueta = "Puerto", SoloNumeros = true, LongitudMaxima = 5 };
    private readonly BotonRedondeado _btnCrear = new BotonRedondeado { Text = "Crear partida", Estilo = EstiloBoton.Principal };
    private readonly BotonRedondeado _btnUnirse = new BotonRedondeado { Text = "Unirse a partida", Estilo = EstiloBoton.Principal };
    private readonly TarjetaPanel _tarjetaJugador = new TarjetaPanel { Titulo = "Su jugador" };
    private readonly Notificaciones _notificaciones;
    private readonly Timer _animacion = new Timer { Interval = 16 };
    private readonly Random _azar = new Random();
    private long _inicioAnimacion;
    private int _dado1 = 1;
    private int _dado2 = 6;
    private int _faseLuces;
    private SesionJuego? _sesionPendiente;

    /// <summary>
    /// Crea la ventana de inicio.
    /// </summary>
    /// <param name="argumentos">Valores iniciales (por ejemplo, desde la línea de comandos).</param>
    public FormularioInicio(ArgumentosInicio argumentos)
    {
        _argumentos = argumentos;
        AutoScaleMode = AutoScaleMode.None;
        Text = Tema.NombreJuego;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(940, 680);
        BackColor = Tema.VerdeMenta;
        Font = Tema.Texto(10f);
        DoubleBuffered = true;
        _notificaciones = new Notificaciones(this);

        // Tarjeta del jugador: nombre a la izquierda; ficha y color a la derecha.
        _tarjetaJugador.SetBounds(16, AltoCabecera, 908, 222);
        _tarjetaJugador.Icono = (g, area) => DibujoFicha.DibujarEnDisco(g, _selectorFicha.Forma, area, Tema.ColorFicha(_selectorFicha.ColorElegido), false);
        _txtNombre.SetBounds(26, 74, 260, 82);
        _tarjetaJugador.Controls.Add(_txtNombre);
        Etiqueta ayudaNombre = new Etiqueta("Así lo verán los demás jugadores;\nsu ficha aparecerá en el tablero.", 9f);
        ayudaNombre.SetBounds(26, 158, 284, 46);
        _tarjetaJugador.Controls.Add(ayudaNombre);
        Etiqueta tituloFicha = new Etiqueta("Elija su ficha", 9.5f, FontStyle.Bold);
        tituloFicha.SetBounds(316, 12, 200, 26);
        _tarjetaJugador.Controls.Add(tituloFicha);
        _selectorFicha.SetBounds(316, 38, 566, 152);
        _selectorFicha.SeleccionCambiada += () => _tarjetaJugador.Invalidate(new Rectangle(0, 0, 90, 80));
        _tarjetaJugador.Controls.Add(_selectorFicha);
        Controls.Add(_tarjetaJugador);

        // Crear partida.
        TarjetaPanel tarjetaCrear = new TarjetaPanel { Titulo = "Crear partida", Icono = (g, a) => Iconos.Banco(g, a, Tema.Rojo) };
        tarjetaCrear.SetBounds(16, 408, 450, 260);
        Etiqueta ayudaCrear = new Etiqueta("Esta computadora será el banco (servidor).", 9f);
        ayudaCrear.SetBounds(26, 68, 400, 24);
        tarjetaCrear.Controls.Add(ayudaCrear);
        _txtMaximoTurnos.SetBounds(26, 96, 190, 82);
        _txtPuertoCrear.SetBounds(232, 96, 190, 82);
        _btnCrear.SetBounds(20, 186, 408, 54);
        _btnCrear.Click += async (s, e) => await CrearPartidaAsync();
        tarjetaCrear.Controls.Add(_txtMaximoTurnos);
        tarjetaCrear.Controls.Add(_txtPuertoCrear);
        tarjetaCrear.Controls.Add(_btnCrear);
        Controls.Add(tarjetaCrear);

        // Unirse a partida.
        TarjetaPanel tarjetaUnirse = new TarjetaPanel { Titulo = "Unirse a partida", Icono = (g, a) => Iconos.Red(g, a, Tema.VerdeProfundo) };
        tarjetaUnirse.SetBounds(474, 408, 450, 260);
        Etiqueta ayudaUnirse = new Etiqueta("Use la dirección que muestra el organizador.", 9f);
        ayudaUnirse.SetBounds(26, 68, 400, 24);
        tarjetaUnirse.Controls.Add(ayudaUnirse);
        _txtIp.SetBounds(26, 96, 250, 82);
        _txtPuertoUnirse.SetBounds(292, 96, 130, 82);
        _btnUnirse.SetBounds(20, 186, 408, 54);
        _btnUnirse.Click += async (s, e) => await UnirseAsync();
        tarjetaUnirse.Controls.Add(_txtIp);
        tarjetaUnirse.Controls.Add(_txtPuertoUnirse);
        tarjetaUnirse.Controls.Add(_btnUnirse);
        Controls.Add(tarjetaUnirse);

        _txtMaximoTurnos.Valor = OpcionesJuego.MaximoTurnosPredeterminado.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _txtPuertoCrear.Valor = Servidor.PuertoPredeterminado.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _txtPuertoUnirse.Valor = _txtPuertoCrear.Valor;
        _txtIp.Valor = "127.0.0.1";
        _txtIp.KeyDown += (s, e) => EnterUne(e);
        _txtPuertoUnirse.KeyDown += (s, e) => EnterUne(e);

        _animacion.Tick += (s, e) => Animar();
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
        _inicioAnimacion = Environment.TickCount64;
        _animacion.Start();
        _txtNombre.Enfocar();
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
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Dibujo.Calidad(g);
        Dibujo.FondoTablero(g, ClientRectangle);

        float t = Math.Min(1f, (Environment.TickCount64 - _inicioAnimacion) / (float)DuracionEntradaMs);
        if (_inicioAnimacion == 0)
        {
            t = 0f;
        }

        // Logotipo: aparece suavemente y baja unos píxeles.
        float aparicion = Suavizar(Math.Min(1f, t / 0.6f));
        RectangleF logo = new RectangleF(200, 14 + ((1f - aparicion) * 14f), 540, 118);
        Logotipo.Dibujar(g, logo, aparicion, _faseLuces);
        using (Font subtitulo = Tema.Texto(11f, FontStyle.Bold))
        {
            Dibujo.TextoCentrado(g, Tema.Subtitulo, subtitulo, Tema.ConOpacidad(Tema.VerdeProfundo, aparicion), new RectangleF(0, 136, ClientSize.Width, 26));
        }

        // Dados que giran y caen junto al logotipo.
        float giro = 1f - Suavizar(t);
        DibujoDado.Dibujar(g, new PointF(118, 78), 62, -16f + (giro * 540f), _dado1, Math.Min(1f, t * 3f));
        DibujoDado.Dibujar(g, new PointF(822, 82), 62, 14f - (giro * 620f), _dado2, Math.Min(1f, t * 3f));
    }

    /// <inheritdoc/>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        // Si se cierra sin haber entrado a la partida, se libera la conexión pendiente.
        _animacion.Dispose();
        _notificaciones.Dispose();
        _sesionPendiente?.Dispose();
        base.OnFormClosed(e);
    }

    private static float Suavizar(float t)
    {
        float inverso = 1f - t;
        return 1f - (inverso * inverso * inverso);
    }

    private void Animar()
    {
        long transcurrido = Environment.TickCount64 - _inicioAnimacion;
        if (transcurrido < DuracionEntradaMs * 0.8f && transcurrido / 90 != (transcurrido - 16) / 90)
        {
            _dado1 = _azar.Next(1, 7);
            _dado2 = _azar.Next(1, 7);
        }
        else if (transcurrido >= DuracionEntradaMs * 0.8f)
        {
            _dado1 = 5;
            _dado2 = 3;
        }

        // Las luces de la marquesina avanzan despacio; la entrada se anima a 60 cuadros por segundo.
        int fase = (int)(transcurrido / 650);
        bool cambiaronLuces = fase != _faseLuces;
        _faseLuces = fase;
        if (transcurrido <= DuracionEntradaMs + 50 || cambiaronLuces)
        {
            Invalidate(new Rectangle(0, 0, ClientSize.Width, AltoCabecera));
        }

        if (transcurrido > DuracionEntradaMs + 50)
        {
            _animacion.Interval = 200;
        }
    }

    private void EnterUne(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            _btnUnirse.PerformClick();
        }
    }

    private async Task CrearPartidaAsync()
    {
        string? nombre = ValidarNombre();
        int? maximo = ValidarEntero(_txtMaximoTurnos, 1, 2000, "Entre 1 y 2000 turnos.");
        int? puerto = ValidarEntero(_txtPuertoCrear, 1024, 65535, "Entre 1024 y 65535.");
        if (nombre == null || maximo == null || puerto == null)
        {
            return;
        }

        Servidor servidor;
        try
        {
            Juego juego = new Juego(new OpcionesJuego { MaximoTurnos = maximo.Value });
            servidor = new Servidor(juego, puerto.Value);
            servidor.Iniciar();
        }
        catch (SocketException ex)
        {
            string detalle = ex.SocketErrorCode == SocketError.AddressAlreadyInUse
                ? $"El puerto {puerto} ya está en uso en esta computadora (¿hay otra partida abierta?). Cierre la otra partida o elija otro puerto."
                : $"No se pudo abrir el puerto {puerto}: {ex.Message}";
            _txtPuertoCrear.MostrarError("Puerto ocupado o no disponible.");
            _notificaciones.Mostrar(detalle, TipoNotificacion.Error, 8000);
            return;
        }

        // El organizador se conecta a su propio servidor como un cliente más.
        await ConectarAsync("127.0.0.1", puerto.Value, nombre, servidor, _btnCrear);
    }

    private async Task UnirseAsync()
    {
        string? nombre = ValidarNombre();
        string ip = _txtIp.Valor.Trim();
        bool ipValida = ip.Length > 0 && EsDireccionValida(ip);
        if (!ipValida)
        {
            _txtIp.MostrarError(ip.Length == 0 ? "Escriba la IP del organizador." : "Debe tener la forma 192.168.1.20.");
        }

        int? puerto = ValidarEntero(_txtPuertoUnirse, 1, 65535, "Entre 1 y 65535.");
        if (nombre == null || !ipValida || puerto == null)
        {
            return;
        }

        await ConectarAsync(ip, puerto.Value, nombre, null, _btnUnirse);
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

    private async Task ConectarAsync(string host, int puerto, string nombre, Servidor? servidor, BotonRedondeado boton)
    {
        string textoBoton = boton.Text;
        HabilitarControles(false);
        boton.Text = "Conectando...";

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
            boton.Text = textoBoton;
        }

        void Fallar(string mensaje)
        {
            Terminar();
            sesion.Dispose();
            MarcarCampoDelError(mensaje);
            _notificaciones.Mostrar(mensaje, TipoNotificacion.Error, 8000);
            HabilitarControles(true);
        }

        alBienvenida = (id, nombreConfirmado) =>
        {
            Terminar();
            SesionCreada?.Invoke(sesion);
        };

        // Rechazos del servidor: partida llena, ya iniciada, nombre o ficha repetidos, etc. (el texto viene del banco).
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

        // Con los argumentos de prueba (--crear/--unirse) el servidor asigna la primera ficha libre, para que
        // varias ventanas abiertas a la vez no choquen con la misma.
        bool automatico = _argumentos.Modo != ModoInicio.Normal;
        Core.Modelo.FormaFicha forma = _selectorFicha.Forma;
        Core.Modelo.ColorFicha color = _selectorFicha.ColorElegido;
        sesion.Solicitar(cliente =>
        {
            if (automatico)
            {
                cliente.Unirse(nombre);
            }
            else
            {
                cliente.Unirse(nombre, forma, color);
            }
        });
    }

    /// <summary>
    /// Marca en rojo el campo al que se refiere un rechazo del servidor (nombre repetido, por ejemplo).
    /// </summary>
    private void MarcarCampoDelError(string mensaje)
    {
        if (mensaje.Contains("llamado", StringComparison.Ordinal) || mensaje.Contains("nombre", StringComparison.OrdinalIgnoreCase))
        {
            _txtNombre.MostrarError("Elija otro nombre.");
        }
    }

    private string? ValidarNombre()
    {
        string nombre = _txtNombre.Valor.Trim();
        if (nombre.Length == 0)
        {
            _txtNombre.MostrarError("Escriba su nombre.");
            _txtNombre.Enfocar();
            return null;
        }

        if (nombre.IndexOf('|') >= 0)
        {
            _txtNombre.MostrarError("No puede contener el carácter |.");
            _txtNombre.Enfocar();
            return null;
        }

        return nombre;
    }

    private static int? ValidarEntero(CampoTexto campo, int minimo, int maximo, string mensaje)
    {
        int? valor = campo.ValorEntero;
        if (valor == null || valor < minimo || valor > maximo)
        {
            campo.MostrarError(mensaje);
            return null;
        }

        return valor;
    }

    private void AplicarArgumentos()
    {
        _txtNombre.Valor = _argumentos.Nombre ?? string.Empty;
        if (_argumentos.Host != null)
        {
            _txtIp.Valor = _argumentos.Host;
        }

        if (_argumentos.Puerto.HasValue)
        {
            string puerto = _argumentos.Puerto.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _txtPuertoCrear.Valor = puerto;
            _txtPuertoUnirse.Valor = puerto;
        }

        if (_argumentos.MaximoTurnos.HasValue)
        {
            _txtMaximoTurnos.Valor = Math.Min(Math.Max(_argumentos.MaximoTurnos.Value, 1), 2000).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private void HabilitarControles(bool habilitar)
    {
        _btnCrear.Enabled = habilitar;
        _btnUnirse.Enabled = habilitar;
        _txtNombre.Enabled = habilitar;
        _selectorFicha.Enabled = habilitar;
    }
}
