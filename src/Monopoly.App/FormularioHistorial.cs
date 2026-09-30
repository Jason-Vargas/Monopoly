using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Monopoly.Core.Estructuras;
using Monopoly.Core.Logica;
using Monopoly.Core.Modelo;
using Monopoly.Core.Red;

namespace Monopoly.App;

/// <summary>
/// Historial de transacciones consultado al servidor, con filtros (todas, más antiguas primero,
/// más recientes primero, por jugador, por tipo) y exportación del TXT.
/// </summary>
internal sealed class FormularioHistorial : Form
{
    private const string PrefijoExportado = "Historial exportado a ";

    private readonly SesionJuego _sesion;
    private readonly ComboBox _cmbFiltro = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbValor = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _btnConsultar = new Button { Text = "Consultar" };
    private readonly Button _btnExportar = new Button { Text = "Exportar TXT" };
    private readonly Button _btnAbrirCarpeta = new Button { Text = "Abrir carpeta", Visible = false };
    private readonly Label _lblResultado = new Label { AutoSize = false };
    private readonly ListView _lista = new ListView { View = View.Details, FullRowSelect = true, GridLines = true };
    private bool _esperandoExportacion;
    private string? _rutaExportada;

    /// <summary>
    /// Crea la ventana de historial.
    /// </summary>
    public FormularioHistorial(SesionJuego sesion)
    {
        _sesion = sesion;
        Text = "Historial de transacciones";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1060, 560);
        MinimumSize = new Size(700, 400);
        BackColor = Paleta.FondoPanel;
        Font = new Font(Paleta.Fuente, 9.5f);

        FlowLayoutPanel barra = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6, 8, 6, 0) };
        barra.Controls.Add(new Label { Text = "Filtro:", AutoSize = true, Margin = new Padding(3, 6, 3, 0) });
        _cmbFiltro.Width = 190;
        _cmbFiltro.Items.Add(new OpcionLista("Todas", "TODAS"));
        _cmbFiltro.Items.Add(new OpcionLista("Más antiguas primero", "ANTIGUAS"));
        _cmbFiltro.Items.Add(new OpcionLista("Más recientes primero", "RECIENTES"));
        _cmbFiltro.Items.Add(new OpcionLista("Por jugador", "JUGADOR"));
        _cmbFiltro.Items.Add(new OpcionLista("Por tipo", "TIPO"));
        _cmbFiltro.SelectedIndex = 0;
        _cmbFiltro.SelectedIndexChanged += (s, e) => CargarValores();
        barra.Controls.Add(_cmbFiltro);
        _cmbValor.Width = 210;
        barra.Controls.Add(_cmbValor);
        _btnConsultar.AutoSize = true;
        _btnConsultar.Click += (s, e) => Consultar();
        barra.Controls.Add(_btnConsultar);
        _btnExportar.AutoSize = true;
        _btnExportar.Click += (s, e) => Exportar();
        barra.Controls.Add(_btnExportar);
        _btnAbrirCarpeta.AutoSize = true;
        _btnAbrirCarpeta.Click += (s, e) => AbrirCarpeta();
        barra.Controls.Add(_btnAbrirCarpeta);

        _lblResultado.Dock = DockStyle.Bottom;
        _lblResultado.Height = 44;
        _lblResultado.Padding = new Padding(8, 4, 8, 4);

        _lista.Dock = DockStyle.Fill;
        _lista.Columns.Add("N°", 50, HorizontalAlignment.Right);
        _lista.Columns.Add("Turno", 62, HorizontalAlignment.Right);
        _lista.Columns.Add("Hora", 80);
        _lista.Columns.Add("Tipo", 205);
        _lista.Columns.Add("Origen", 100);
        _lista.Columns.Add("Destino", 100);
        _lista.Columns.Add("Monto", 80, HorizontalAlignment.Right);
        _lista.Columns.Add("Descripción", 330);

        Controls.Add(_lista);
        Controls.Add(_lblResultado);
        Controls.Add(barra);

        _sesion.TransaccionesRecibidas += AlRecibirTransacciones;
        _sesion.EventoRecibido += AlRecibirEvento;
        _sesion.ErrorRecibido += AlRecibirError;
        CargarValores();
    }

    /// <inheritdoc/>
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Consultar();
    }

    /// <inheritdoc/>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _sesion.TransaccionesRecibidas -= AlRecibirTransacciones;
        _sesion.EventoRecibido -= AlRecibirEvento;
        _sesion.ErrorRecibido -= AlRecibirError;
        base.OnFormClosed(e);
    }

    private void CargarValores()
    {
        string filtro = ((OpcionLista)_cmbFiltro.SelectedItem!).Valor;
        _cmbValor.Items.Clear();
        if (filtro == "JUGADOR" && _sesion.UltimoEstado != null)
        {
            foreach (EstadoJugador jugador in _sesion.UltimoEstado.Instantanea.Jugadores)
            {
                _cmbValor.Items.Add(new OpcionLista(jugador.Nombre, jugador.Nombre));
            }

            _cmbValor.Items.Add(new OpcionLista("Banco", Transaccion.Banco));
        }
        else if (filtro == "TIPO")
        {
            foreach (TipoTransaccion tipo in Enum.GetValues<TipoTransaccion>())
            {
                _cmbValor.Items.Add(new OpcionLista(Formato.Nombre(tipo), tipo.ToString()));
            }
        }

        _cmbValor.Enabled = _cmbValor.Items.Count > 0;
        if (_cmbValor.Enabled)
        {
            _cmbValor.SelectedIndex = 0;
        }
    }

    private void Consultar()
    {
        FiltroTransacciones filtro = Enum.Parse<FiltroTransacciones>(((OpcionLista)_cmbFiltro.SelectedItem!).Valor, true);
        string? valor = _cmbValor.Enabled ? ((OpcionLista)_cmbValor.SelectedItem!).Valor : null;
        _lblResultado.ForeColor = Color.DimGray;
        _lblResultado.Text = "Consultando al servidor...";
        _sesion.Solicitar(cliente => cliente.ConsultarTransacciones(filtro, valor));
    }

    private void Exportar()
    {
        _esperandoExportacion = true;
        _lblResultado.ForeColor = Color.DimGray;
        _lblResultado.Text = "Solicitando la exportación al servidor...";
        _sesion.Solicitar(cliente => cliente.ExportarTransacciones());
    }

    private void AlRecibirTransacciones(string filtro, ListaSimple<Transaccion> transacciones)
    {
        _lista.BeginUpdate();
        _lista.Items.Clear();
        transacciones.Recorrer(t =>
        {
            ListViewItem fila = new ListViewItem(t.Id.ToString(CultureInfo.InvariantCulture));
            fila.SubItems.Add(t.NumeroTurno.ToString(CultureInfo.InvariantCulture));
            fila.SubItems.Add(t.FechaHora.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
            fila.SubItems.Add(Formato.Nombre(t.Tipo));
            fila.SubItems.Add(t.Origen);
            fila.SubItems.Add(t.Destino);
            fila.SubItems.Add(Formato.Dinero(t.Monto));
            fila.SubItems.Add(t.Descripcion);
            _lista.Items.Add(fila);
        });
        _lista.EndUpdate();
        _lblResultado.ForeColor = Color.Black;
        _lblResultado.Text = $"{transacciones.Cantidad} transacción(es) · filtro: {filtro}";
    }

    private void AlRecibirEvento(string texto)
    {
        if (!_esperandoExportacion || !texto.StartsWith(PrefijoExportado, StringComparison.Ordinal))
        {
            return;
        }

        _esperandoExportacion = false;
        _rutaExportada = texto.Substring(PrefijoExportado.Length).TrimEnd('.');
        _lblResultado.ForeColor = Color.FromArgb(20, 110, 40);
        _lblResultado.Text = _sesion.EsOrganizador
            ? $"TXT generado en esta computadora: {_rutaExportada}"
            : $"TXT generado en la computadora del organizador (banco): {_rutaExportada}";
        _btnAbrirCarpeta.Visible = _sesion.EsOrganizador;
    }

    private void AlRecibirError(string mensaje)
    {
        _esperandoExportacion = false;
        _lblResultado.ForeColor = Color.FromArgb(170, 20, 20);
        _lblResultado.Text = mensaje;
    }

    private void AbrirCarpeta()
    {
        if (_rutaExportada == null || !File.Exists(_rutaExportada))
        {
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_rutaExportada}\"") { UseShellExecute = true });
    }
}
