using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Monopoly.Core.Hardware;

namespace Monopoly.App;

/// <summary>
/// Barra del organizador para el cajero (Raspberry Pi Pico W por USB): selector de puerto COM,
/// conectar/desconectar, detección automática (PING/PONG en cada puerto) e indicador de estado.
/// Sin la Pico, el juego sigue en modo simulado.
/// </summary>
internal sealed class BarraCajero : ToolStrip
{
    private readonly SesionJuego _sesion;
    private readonly ToolStripComboBox _cmbPuertos = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
    private readonly ToolStripButton _btnActualizar = new ToolStripButton("↻") { ToolTipText = "Actualizar la lista de puertos" };
    private readonly ToolStripButton _btnConectar = new ToolStripButton("Conectar");
    private readonly ToolStripButton _btnDetectar = new ToolStripButton("Detectar") { ToolTipText = "Probar cada puerto con PING/PONG" };
    private readonly ToolStripLabel _lblEstado = new ToolStripLabel();
    private bool _ocupado;

    /// <summary>
    /// Crea la barra para la sesión del organizador.
    /// </summary>
    public BarraCajero(SesionJuego sesion)
    {
        _sesion = sesion;
        GripStyle = ToolStripGripStyle.Hidden;
        Padding = new Padding(6, 2, 6, 2);
        Font = new Font(Paleta.Fuente, 9.5f);
        BackColor = Color.FromArgb(232, 240, 232);

        Items.Add(new ToolStripLabel("Cajero (Pico W):") { Font = new Font(Paleta.Fuente, 9.5f, FontStyle.Bold) });
        Items.Add(_cmbPuertos);
        Items.Add(_btnActualizar);
        Items.Add(new ToolStripSeparator());
        Items.Add(_btnConectar);
        Items.Add(_btnDetectar);
        Items.Add(new ToolStripSeparator());
        Items.Add(_lblEstado);

        _btnActualizar.Click += (s, e) => CargarPuertos();
        _btnConectar.Click += async (s, e) => await ConectarODesconectarAsync();
        _btnDetectar.Click += async (s, e) => await DetectarAsync();
        _sesion.CajeroCambiado += Actualizar;

        CargarPuertos();
        Actualizar();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sesion.CajeroCambiado -= Actualizar;
        }

        base.Dispose(disposing);
    }

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
        else if (_cmbPuertos.Items.Count > 0)
        {
            _cmbPuertos.SelectedIndex = _cmbPuertos.Items.Count - 1;
        }

        Actualizar();
    }

    private async Task ConectarODesconectarAsync()
    {
        if (PicoConectada)
        {
            _sesion.CambiarCajero(new CajeroSimulado());
            return;
        }

        if (_cmbPuertos.SelectedItem is not string puerto)
        {
            MostrarError("No hay puertos COM. Conecte la Pico W por USB y pulse ↻, o use \"Detectar\".");
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
        }
        catch (Exception ex) when (ex is IOException || ex is TimeoutException)
        {
            pico.Dispose();
            MostrarError(ex.Message);
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
        _sesion.CambiarCajero(new CajeroSimulado());
        Ocupado(true, "Buscando la Pico W en los puertos COM...");
        string? puerto = await Task.Run(() => CajeroPico.DetectarPuerto());
        Ocupado(false, null);
        CargarPuertos();
        if (puerto == null)
        {
            MostrarError("No se encontró ninguna Pico W. Revise el cable USB, que main.py esté cargado y que Thonny esté cerrado.");
            return;
        }

        _cmbPuertos.SelectedItem = puerto;
        await ConectarAsync(puerto);
    }

    private void Ocupado(bool ocupado, string? mensaje)
    {
        _ocupado = ocupado;
        if (mensaje != null)
        {
            _lblEstado.ForeColor = Color.DimGray;
            _lblEstado.Text = mensaje;
        }

        Actualizar();
    }

    private void MostrarError(string mensaje)
    {
        Actualizar();
        _lblEstado.ForeColor = Color.FromArgb(170, 20, 20);
        _lblEstado.Text = "⚠ " + mensaje;
        MessageBox.Show(FindForm(), mensaje, "Cajero (Pico W)", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void Actualizar()
    {
        IDispositivoCajero cajero = _sesion.Cajero;
        bool conectada = PicoConectada;
        _btnConectar.Text = conectada ? "Desconectar" : "Conectar";
        _btnConectar.Enabled = !_ocupado && (conectada || _cmbPuertos.Items.Count > 0);
        _btnDetectar.Enabled = !_ocupado && !conectada;
        _cmbPuertos.Enabled = !_ocupado && !conectada;
        _btnActualizar.Enabled = !_ocupado && !conectada;
        if (_ocupado)
        {
            return;
        }

        if (conectada)
        {
            _lblEstado.ForeColor = Color.FromArgb(20, 120, 40);
            _lblEstado.Text = $"● {cajero.Descripcion}: botón de dados y pago con tarjeta";
        }
        else if (cajero.EsFisico)
        {
            _lblEstado.ForeColor = Color.FromArgb(200, 100, 0);
            _lblEstado.Text = $"● {cajero.Descripcion} desconectado: modo simulado (pulse Conectar para reintentar)";
        }
        else
        {
            _lblEstado.ForeColor = Color.DimGray;
            _lblEstado.Text = "○ Sin cajero: modo simulado";
        }
    }
}
