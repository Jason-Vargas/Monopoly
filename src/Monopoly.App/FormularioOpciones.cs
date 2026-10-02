using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Monopoly.App.Estilo;
using Monopoly.Core.Hardware;
using Monopoly.Core.Logica;
using Monopoly.Core.Red;

namespace Monopoly.App;

/// <summary>
/// Opciones (se abren con el engranaje): la sección de la Raspberry Pi Pico W (solo el organizador, que la
/// tiene conectada por USB: estado, puerto COM, detectar/conectar/desconectar, probar el LED, monitor de
/// las líneas recibidas, tarjetas vinculadas y el modo sin hardware con sus simulaciones), el sonido
/// (activar y volumen) y la partida (dirección del servidor, turno y salir con confirmación).
/// </summary>
internal sealed class FormularioOpciones : Form
{
    private const int AnchoSeccion = 580;

    private readonly SesionJuego _sesion;
    private readonly Action _salir;
    private readonly Panel _contenido = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

    // Pico W.
    private readonly Etiqueta _lblPico = new Etiqueta(string.Empty, 10.5f, FontStyle.Bold);
    private readonly ComboBox _cmbPuertos = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
    private readonly BotonRedondeado _btnActualizar = new BotonRedondeado { Text = "↻", Estilo = EstiloBoton.Secundario, Radio = 9f };
    private readonly BotonRedondeado _btnDetectar = new BotonRedondeado { Text = "Detectar automáticamente", Estilo = EstiloBoton.Secundario };
    private readonly BotonRedondeado _btnConectar = new BotonRedondeado { Text = "Conectar" };
    private readonly BotonRedondeado _btnDesconectar = new BotonRedondeado { Text = "Desconectar", Estilo = EstiloBoton.Secundario };
    private readonly BotonRedondeado _btnProbarLed = new BotonRedondeado { Text = "Probar LED", Estilo = EstiloBoton.Secundario };
    private readonly ListBox _lstMonitor = new ListBox { IntegralHeight = false, BorderStyle = BorderStyle.None };
    private readonly Lienzo _tarjetas = new Lienzo();
    private readonly CheckBox _chkSinHardware = new CheckBox { Text = "Modo sin hardware (pruebas)", AutoSize = true, BackColor = Color.Transparent };
    private readonly BotonRedondeado _btnSimularBoton = new BotonRedondeado { Text = "Simular botón", Estilo = EstiloBoton.Exito };
    private readonly BotonRedondeado _btnSimularTarjeta = new BotonRedondeado { Text = "Simular tarjeta del jugador en turno", Estilo = EstiloBoton.Exito };

    // Sonido.
    private readonly CheckBox _chkSonido = new CheckBox { Text = "Activar sonidos", AutoSize = true, BackColor = Color.Transparent };
    private readonly TrackBar _volumen = new TrackBar { Minimum = 0, Maximum = 100, TickFrequency = 10, AutoSize = false };
    private readonly Etiqueta _lblVolumen = new Etiqueta(string.Empty, 10f, FontStyle.Bold, Tema.Tinta);

    // Partida.
    private readonly Etiqueta _lblPartida = new Etiqueta(string.Empty, 10f, FontStyle.Regular, Tema.Tinta);

    private readonly Notificaciones _notificaciones;
    private bool _ocupado;

    /// <summary>
    /// Crea la ventana de opciones.
    /// </summary>
    /// <param name="sesion">Sesión del jugador.</param>
    /// <param name="salir">Acción que sale de la partida (la ventana principal pide confirmación).</param>
    public FormularioOpciones(SesionJuego sesion, Action salir)
    {
        _sesion = sesion;
        _salir = salir;
        AutoScaleMode = AutoScaleMode.None;
        Text = $"Opciones · {Tema.NombreJuego}";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(AnchoSeccion + 48, 760);
        BackColor = Tema.Crema;
        Font = Tema.Texto(10f);
        _notificaciones = new Notificaciones(this);
        Controls.Add(_contenido);

        int y = 12;
        if (sesion.EsOrganizador)
        {
            y = CrearSeccionPico(y) + 12;
        }

        y = CrearSeccionSonido(y) + 12;
        CrearSeccionPartida(y);

        _sesion.CajeroCambiado += ActualizarPico;
        _sesion.EstadoActualizado += AlActualizarEstado;
        if (_sesion.MonitorCajero != null)
        {
            _sesion.MonitorCajero.LineaAgregada += AgregarAlMonitor;
            _sesion.MonitorCajero.RecorrerLineas(linea => _lstMonitor.Items.Add(linea));
            DesplazarMonitor();
        }

        CargarPuertos();
        ActualizarPico();
        ActualizarPartida();
    }

    /// <inheritdoc/>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _sesion.CajeroCambiado -= ActualizarPico;
        _sesion.EstadoActualizado -= AlActualizarEstado;
        if (_sesion.MonitorCajero != null)
        {
            _sesion.MonitorCajero.LineaAgregada -= AgregarAlMonitor;
        }

        _notificaciones.Dispose();
        base.OnFormClosed(e);
    }

    // ------------------------------------------------------------------ secciones

    private int CrearSeccionPico(int y)
    {
        TarjetaPanel seccion = Seccion("Raspberry Pi Pico W", (g, a) => Iconos.Chip(g, a, Tema.Rojo), y, 656);
        foreach (BotonRedondeado boton in new[] { _btnActualizar, _btnDetectar, _btnConectar, _btnDesconectar, _btnProbarLed, _btnSimularBoton, _btnSimularTarjeta })
        {
            boton.Font = Tema.Texto(9.5f, FontStyle.Bold);
        }

        _lblPico.SetBounds(24, 62, AnchoSeccion - 48, 28);
        seccion.Controls.Add(_lblPico);

        seccion.Controls.Add(Rotulo("Puerto COM", 24, 96));
        _cmbPuertos.Font = Tema.Texto(10.5f);
        _cmbPuertos.SetBounds(24, 122, 140, 30);
        seccion.Controls.Add(_cmbPuertos);
        _btnActualizar.SetBounds(170, 114, 54, 48);
        _btnActualizar.Click += (s, e) => CargarPuertos();
        seccion.Controls.Add(_btnActualizar);
        _btnDetectar.SetBounds(228, 112, 330, 52);
        _btnDetectar.Click += async (s, e) => await DetectarAsync();
        seccion.Controls.Add(_btnDetectar);

        _btnConectar.SetBounds(18, 168, 178, 52);
        _btnConectar.Click += async (s, e) => await ConectarSeleccionadoAsync();
        _btnDesconectar.SetBounds(199, 168, 178, 52);
        _btnDesconectar.Click += (s, e) => Desconectar();
        _btnProbarLed.SetBounds(380, 168, 178, 52);
        _btnProbarLed.Click += (s, e) => ProbarLed();
        seccion.Controls.Add(_btnConectar);
        seccion.Controls.Add(_btnDesconectar);
        seccion.Controls.Add(_btnProbarLed);

        seccion.Controls.Add(Rotulo("Monitor (últimas líneas de la Pico)", 24, 224));
        _lstMonitor.Font = Tema.Monoespaciada(9.5f);
        _lstMonitor.BackColor = Color.FromArgb(28, 36, 30);
        _lstMonitor.ForeColor = Color.FromArgb(170, 235, 180);
        _lstMonitor.SetBounds(24, 248, AnchoSeccion - 48, 132);
        seccion.Controls.Add(_lstMonitor);

        seccion.Controls.Add(Rotulo("Tarjetas vinculadas", 24, 392));
        _tarjetas.SetBounds(24, 418, AnchoSeccion - 48, 4 * 30);
        _tarjetas.Paint += (s, e) => DibujarTarjetasVinculadas(e.Graphics);
        seccion.Controls.Add(_tarjetas);

        _chkSinHardware.Font = Tema.Texto(10.5f, FontStyle.Bold);
        _chkSinHardware.ForeColor = Tema.Tinta;
        _chkSinHardware.Location = new Point(24, 546);
        _chkSinHardware.Checked = _sesion.ModoSinHardware;
        _chkSinHardware.CheckedChanged += (s, e) => CambiarModo();
        seccion.Controls.Add(_chkSinHardware);
        _btnSimularBoton.SetBounds(18, 580, 190, 52);
        _btnSimularBoton.Click += (s, e) => _sesion.SimularBoton();
        _btnSimularTarjeta.SetBounds(211, 580, 347, 52);
        _btnSimularTarjeta.Click += (s, e) => _sesion.SimularTarjetaDelJugadorEnTurno();
        seccion.Controls.Add(_btnSimularBoton);
        seccion.Controls.Add(_btnSimularTarjeta);
        return seccion.Bottom;
    }

    private int CrearSeccionSonido(int y)
    {
        TarjetaPanel seccion = Seccion("Sonido", (g, a) => Iconos.Altavoz(g, a, Tema.VerdeProfundo), y, 176);
        _chkSonido.Font = Tema.Texto(10.5f, FontStyle.Bold);
        _chkSonido.ForeColor = Tema.Tinta;
        _chkSonido.Location = new Point(24, 64);
        _chkSonido.Checked = Preferencias.SonidoActivado;
        _chkSonido.CheckedChanged += (s, e) =>
        {
            Preferencias.SonidoActivado = _chkSonido.Checked;
            _volumen.Enabled = _chkSonido.Checked;
        };
        seccion.Controls.Add(_chkSonido);

        seccion.Controls.Add(Rotulo("Volumen", 24, 100));
        _volumen.SetBounds(20, 120, AnchoSeccion - 120, 36);
        _volumen.BackColor = Tema.Marfil;
        _volumen.Value = Preferencias.Volumen;
        _volumen.Enabled = Preferencias.SonidoActivado;
        _volumen.ValueChanged += (s, e) =>
        {
            Preferencias.Volumen = _volumen.Value;
            _lblVolumen.Text = $"{_volumen.Value} %";
        };
        seccion.Controls.Add(_volumen);
        _lblVolumen.SetBounds(AnchoSeccion - 92, 122, 64, 26);
        _lblVolumen.Text = $"{_volumen.Value} %";
        seccion.Controls.Add(_lblVolumen);
        return seccion.Bottom;
    }

    private void CrearSeccionPartida(int y)
    {
        TarjetaPanel seccion = Seccion("Partida", (g, a) => Iconos.Banco(g, a, Tema.Rojo), y, 240);
        _lblPartida.SetBounds(24, 60, AnchoSeccion - 48, 100);
        seccion.Controls.Add(_lblPartida);
        BotonRedondeado salir = new BotonRedondeado { Text = "Salir de la partida" };
        salir.SetBounds(18, 166, 250, 52);
        salir.Click += (s, e) =>
        {
            Close();
            _salir();
        };
        seccion.Controls.Add(salir);
    }

    private TarjetaPanel Seccion(string titulo, Action<Graphics, RectangleF> icono, int y, int alto)
    {
        TarjetaPanel seccion = new TarjetaPanel { Titulo = titulo, TamanioTitulo = 14f, Icono = icono };
        seccion.SetBounds(12, y, AnchoSeccion + 8, alto);
        _contenido.Controls.Add(seccion);
        return seccion;
    }

    private static Etiqueta Rotulo(string texto, int x, int y)
    {
        Etiqueta rotulo = new Etiqueta(texto, 9.5f, FontStyle.Bold);
        rotulo.SetBounds(x, y, AnchoSeccion - 48, 22);
        return rotulo;
    }

    // ------------------------------------------------------------------ Pico W

    private bool PicoConectada => _sesion.Cajero.EsFisico && _sesion.Cajero.Estado == EstadoCajero.Conectado;

    private void CargarPuertos()
    {
        string? seleccionado = _cmbPuertos.SelectedItem as string;
        _cmbPuertos.Items.Clear();
        foreach (string puerto in CajeroPico.PuertosDisponibles())
        {
            _cmbPuertos.Items.Add(puerto);
        }

        if (seleccionado != null && _cmbPuertos.Items.Contains(seleccionado))
        {
            _cmbPuertos.SelectedItem = seleccionado;
        }
        else if (_sesion.Cajero.EsFisico && _sesion.Cajero is CajeroPico actual && _cmbPuertos.Items.Contains(actual.NombrePuerto))
        {
            _cmbPuertos.SelectedItem = actual.NombrePuerto;
        }
        else if (_cmbPuertos.Items.Count > 0)
        {
            _cmbPuertos.SelectedIndex = _cmbPuertos.Items.Count - 1;
        }

        ActualizarPico();
    }

    private async Task ConectarSeleccionadoAsync()
    {
        if (_cmbPuertos.SelectedItem is not string puerto)
        {
            _notificaciones.Mostrar("No hay puertos COM. Conecte la Pico W por USB y pulse ↻, o use \"Detectar automáticamente\".", TipoNotificacion.Error, 6000);
            return;
        }

        await ConectarAsync(puerto);
    }

    private async Task ConectarAsync(string puerto)
    {
        Ocupado(true, $"Conectando a {puerto}...");
        CajeroPico pico = new CajeroPico(puerto);
        try
        {
            await Task.Run(() => pico.Conectar());
            _sesion.CambiarCajero(pico);
            _notificaciones.Mostrar($"Pico W conectada en {puerto}.", TipoNotificacion.Exito);
        }
        catch (Exception ex) when (ex is IOException || ex is TimeoutException)
        {
            pico.Dispose();
            _notificaciones.Mostrar(ex.Message, TipoNotificacion.Error, 8000);
        }
        finally
        {
            Ocupado(false, null);
        }
    }

    private async Task DetectarAsync()
    {
        if (PicoConectada)
        {
            return;
        }

        // Si hay una Pico que se desconectó, se libera su puerto antes de probar.
        _sesion.MonitorCajero?.MarcarDesconexionPedida();
        _sesion.CambiarCajero(new CajeroSimulado());
        Ocupado(true, "Buscando la Pico W en los puertos COM...");
        string? puerto = await Task.Run(() => CajeroPico.DetectarPuerto());
        Ocupado(false, null);
        CargarPuertos();
        if (puerto == null)
        {
            _notificaciones.Mostrar("No se encontró ninguna Pico W. Revise el cable USB, que main.py esté cargado y que Thonny esté cerrado.", TipoNotificacion.Error, 8000);
            return;
        }

        _cmbPuertos.SelectedItem = puerto;
        await ConectarAsync(puerto);
    }

    private void Desconectar()
    {
        if (!_sesion.Cajero.EsFisico)
        {
            return;
        }

        _sesion.MonitorCajero?.MarcarDesconexionPedida();
        _sesion.CambiarCajero(new CajeroSimulado());
        _notificaciones.Mostrar("Pico W desconectada. La partida queda en pausa hasta volver a conectarla.", TipoNotificacion.Informacion);
    }

    private void ProbarLed()
    {
        _sesion.Cajero.IndicarPago(true);
        _sesion.MonitorCajero?.Registrar("→ PAGO_OK (prueba del LED)");
    }

    private void CambiarModo()
    {
        if (_chkSinHardware.Checked && PicoConectada)
        {
            _sesion.MonitorCajero?.MarcarDesconexionPedida();
        }

        _sesion.EstablecerModoSinHardware(_chkSinHardware.Checked);
        ActualizarPico();
    }

    private void Ocupado(bool ocupado, string? mensaje)
    {
        _ocupado = ocupado;
        ActualizarPico();
        if (mensaje != null)
        {
            _lblPico.ColorIndicador = Tema.Informacion;
            _lblPico.ForeColor = Tema.Informacion;
            _lblPico.Text = mensaje;
        }
    }

    private void ActualizarPico()
    {
        if (!_sesion.EsOrganizador)
        {
            return;
        }

        bool sinHardware = _sesion.ModoSinHardware;
        bool conectada = PicoConectada;
        bool hayDesconectada = _sesion.Cajero.EsFisico && !conectada;
        _cmbPuertos.Enabled = !_ocupado && !sinHardware && !conectada;
        _btnActualizar.Enabled = !_ocupado && !sinHardware && !conectada;
        _btnDetectar.Enabled = !_ocupado && !sinHardware && !conectada;
        _btnConectar.Enabled = !_ocupado && !sinHardware && !conectada && _cmbPuertos.Items.Count > 0;
        _btnDesconectar.Enabled = !_ocupado && (conectada || hayDesconectada);
        _btnProbarLed.Enabled = !_ocupado && conectada;
        _chkSinHardware.Enabled = !_ocupado;
        _btnSimularBoton.Visible = sinHardware;
        _btnSimularTarjeta.Visible = sinHardware;
        if (_chkSinHardware.Checked != sinHardware)
        {
            _chkSinHardware.Checked = sinHardware;
        }

        if (_ocupado)
        {
            return;
        }

        Color color = sinHardware ? Tema.Informacion : conectada ? Tema.Exito : Tema.Error;
        _lblPico.ColorIndicador = color;
        _lblPico.ForeColor = color;
        _lblPico.Text = sinHardware ? "Modo sin hardware: use las simulaciones de abajo"
            : conectada ? $"Conectada · {_sesion.Cajero.Descripcion}"
            : "Desconectada: la partida espera a la Pico W";
        _lblPico.Invalidate();
        _tarjetas.Invalidate();
    }

    private void AgregarAlMonitor(string linea)
    {
        _lstMonitor.Items.Add(linea);
        while (_lstMonitor.Items.Count > 40)
        {
            _lstMonitor.Items.RemoveAt(0);
        }

        DesplazarMonitor();
    }

    private void DesplazarMonitor()
    {
        _lstMonitor.TopIndex = Math.Max(0, _lstMonitor.Items.Count - 1);
    }

    private void DibujarTarjetasVinculadas(Graphics g)
    {
        Dibujo.Calidad(g);
        g.Clear(Tema.Marfil);
        EstadoJugador[] jugadores = _sesion.UltimoEstado?.Instantanea.Jugadores ?? new EstadoJugador[0];
        using Font nombre = Tema.Texto(10f, FontStyle.Bold);
        using Font estado = Tema.Texto(9.5f, FontStyle.Bold);
        using SolidBrush tinta = new SolidBrush(Tema.Tinta);
        for (int i = 0; i < jugadores.Length; i++)
        {
            EstadoJugador j = jugadores[i];
            float y = i * 30;
            DibujoFicha.DibujarEnDisco(g, j.FormaFicha, new RectangleF(2, y + 2, 26, 26), Paleta.ColorFicha(j.ColorFicha), false);
            g.DrawString(j.Nombre, nombre, tinta, 36, y + 3);
            Color color = j.TieneTarjetaFisica ? Tema.Exito : Tema.Advertencia;
            RectangleF circulo = new RectangleF(_tarjetas.Width - 150, y + 7, 16, 16);
            using (SolidBrush relleno = new SolidBrush(color))
            {
                g.FillEllipse(relleno, circulo);
            }

            if (j.TieneTarjetaFisica)
            {
                Dibujo.Verificacion(g, RectangleF.Inflate(circulo, -3.5f, -3.5f), Color.White, 1.8f);
            }

            using SolidBrush colorEstado = new SolidBrush(color);
            g.DrawString(j.TieneTarjetaFisica ? "vinculada" : "pendiente", estado, colorEstado, _tarjetas.Width - 128, y + 4);
        }

        if (jugadores.Length == 0)
        {
            using SolidBrush suave = new SolidBrush(Tema.TintaSuave);
            g.DrawString("Todavía no hay jugadores.", nombre, suave, 2, 4);
        }
    }

    // ------------------------------------------------------------------ partida

    private void AlActualizarEstado(EstadoRed estado)
    {
        ActualizarPartida();
        _tarjetas.Invalidate();
    }

    private void ActualizarPartida()
    {
        (string principal, string otras) = FormularioSalaEspera.Direcciones(_sesion);
        InstantaneaJuego? i = _sesion.UltimoEstado?.Instantanea;
        string turno = i == null || i.Estado == EstadoPartida.EsperandoJugadores
            ? "La partida todavía no comenzó."
            : i.Estado == EstadoPartida.Finalizada ? "La partida terminó." : $"Turno {i.NumeroTurno} de {i.MaximoTurnos}";
        string rol = _sesion.EsOrganizador ? "organizador (aloja al banco)" : "jugador";
        _lblPartida.Text = $"Servidor: {principal}\n{(_sesion.EsOrganizador ? otras.Replace("\n", " · ") : $"Conectado como {_sesion.Nombre}")}\n" +
                           $"Usted: {_sesion.Nombre}, {rol}\n{turno}";
    }
}
