using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Monopoly.App.Estilo;
using Monopoly.Core.Estructuras;
using Monopoly.Core.Logica;
using Monopoly.Core.Modelo;
using Monopoly.Core.Red;

namespace Monopoly.App;

/// <summary>
/// Historial de transacciones en una ventana aparte: tabla con filas alternadas, ícono por tipo y montos en
/// verde (ingreso) o rojo (pago). Filtros: todas, más antiguas primero, más recientes primero, por jugador y
/// por tipo; cada filtro se consulta al servidor con CONSULTAR_TRANSACCIONES. Exporta el TXT y se actualiza
/// sola cuando llegan transacciones nuevas mientras está abierta.
/// </summary>
internal sealed class FormularioHistorial : Form
{
    private const string PrefijoExportado = "Historial exportado a ";
    private const int AltoCabecera = 128;

    private static readonly string[] NombresFiltro = { "Todas", "Más antiguas primero", "Más recientes primero", "Por jugador", "Por tipo" };
    private static readonly FiltroTransacciones[] Filtros =
    {
        FiltroTransacciones.Todas, FiltroTransacciones.Antiguas, FiltroTransacciones.Recientes, FiltroTransacciones.Jugador, FiltroTransacciones.Tipo,
    };

    private readonly SesionJuego _sesion;
    private readonly BotonRedondeado[] _botonesFiltro = new BotonRedondeado[Filtros.Length];
    private readonly ComboBox _cmbValor = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
    private readonly BotonRedondeado _btnExportar = new BotonRedondeado { Text = "Exportar TXT", Estilo = EstiloBoton.Exito };
    private readonly BotonRedondeado _btnAbrirCarpeta = new BotonRedondeado { Text = "Abrir carpeta", Estilo = EstiloBoton.Secundario, Visible = false };
    private readonly TablaTransacciones _tabla = new TablaTransacciones();
    private readonly Etiqueta _lblResultado = new Etiqueta(string.Empty, 9.5f);
    private readonly Notificaciones _notificaciones;
    private int _filtro;
    private int _ultimaCantidad = -1;
    private bool _esperandoExportacion;
    private bool _esperandoConsulta;
    private bool _cargandoValores;
    private string? _rutaExportada;

    /// <summary>
    /// Crea la ventana de historial.
    /// </summary>
    public FormularioHistorial(SesionJuego sesion)
    {
        _sesion = sesion;
        AutoScaleMode = AutoScaleMode.None;
        Text = $"Historial de transacciones · {Tema.NombreJuego}";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1260, 720);
        MinimumSize = new Size(900, 480);
        BackColor = Tema.Crema;
        Font = Tema.Texto(10f);
        DoubleBuffered = true;
        _notificaciones = new Notificaciones(this);

        int x = 20;
        for (int i = 0; i < Filtros.Length; i++)
        {
            int indice = i;
            BotonRedondeado boton = new BotonRedondeado { Text = NombresFiltro[i], Estilo = EstiloBoton.Secundario, Radio = 10f };
            boton.Font = Tema.Texto(9.5f, FontStyle.Bold);
            int ancho = TextRenderer.MeasureText(NombresFiltro[i], boton.Font).Width + 34;
            boton.SetBounds(x, 66, ancho, 46);
            boton.Click += (s, e) => ElegirFiltro(indice);
            _botonesFiltro[i] = boton;
            Controls.Add(boton);
            x += ancho + 2;
        }

        _cmbValor.Font = Tema.Texto(10.5f);
        _cmbValor.SetBounds(x + 6, 76, 190, 30);
        _cmbValor.SelectedIndexChanged += AlCambiarValor;
        Controls.Add(_cmbValor);

        _btnExportar.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnExportar.Font = Tema.Texto(10f, FontStyle.Bold);
        _btnExportar.SetBounds(ClientSize.Width - 200, 6, 184, 52);
        _btnExportar.Click += (s, e) => Exportar();
        Controls.Add(_btnExportar);
        _btnAbrirCarpeta.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnAbrirCarpeta.Font = Tema.Texto(10f, FontStyle.Bold);
        _btnAbrirCarpeta.SetBounds(ClientSize.Width - 390, 6, 184, 52);
        _btnAbrirCarpeta.Click += (s, e) => AbrirCarpeta();
        Controls.Add(_btnAbrirCarpeta);

        _tabla.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _tabla.SetBounds(20, AltoCabecera, ClientSize.Width - 40, ClientSize.Height - AltoCabecera - 48);
        Controls.Add(_tabla);

        _lblResultado.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _lblResultado.SetBounds(20, ClientSize.Height - 42, ClientSize.Width - 40, 34);
        _lblResultado.TextAlign = ContentAlignment.MiddleLeft;
        Controls.Add(_lblResultado);

        _sesion.TransaccionesRecibidas += AlRecibirTransacciones;
        _sesion.EventoRecibido += AlRecibirEvento;
        _sesion.ErrorRecibido += AlRecibirError;
        _sesion.EstadoActualizado += AlActualizarEstado;
        _ultimaCantidad = _sesion.UltimoEstado?.Instantanea.CantidadTransacciones ?? 0;
        ElegirFiltro(0, false);
    }

    /// <inheritdoc/>
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Consultar();
    }

    /// <inheritdoc/>
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Dibujo.Calidad(g);
        using (SolidBrush franja = new SolidBrush(Tema.VerdeMenta))
        {
            g.FillRectangle(franja, 0, 0, ClientSize.Width, 58);
        }

        Iconos.Libro(g, new RectangleF(20, 14, 32, 32), Tema.Rojo);
        using Font titulo = Tema.Titulo(19f);
        using SolidBrush rojo = new SolidBrush(Tema.Rojo);
        g.DrawString("Historial de transacciones", titulo, rojo, 62, 10);
    }

    /// <inheritdoc/>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _sesion.TransaccionesRecibidas -= AlRecibirTransacciones;
        _sesion.EventoRecibido -= AlRecibirEvento;
        _sesion.ErrorRecibido -= AlRecibirError;
        _sesion.EstadoActualizado -= AlActualizarEstado;
        _notificaciones.Dispose();
        base.OnFormClosed(e);
    }

    private void ElegirFiltro(int indice, bool consultar = true)
    {
        _filtro = indice;
        for (int i = 0; i < _botonesFiltro.Length; i++)
        {
            _botonesFiltro[i].Estilo = i == indice ? EstiloBoton.Principal : EstiloBoton.Secundario;
        }

        CargarValores();
        if (consultar)
        {
            Consultar();
        }
    }

    private void CargarValores()
    {
        FiltroTransacciones filtro = Filtros[_filtro];
        _cmbValor.BeginUpdate();
        _cmbValor.Items.Clear();
        if (filtro == FiltroTransacciones.Jugador && _sesion.UltimoEstado != null)
        {
            foreach (EstadoJugador jugador in _sesion.UltimoEstado.Instantanea.Jugadores)
            {
                _cmbValor.Items.Add(new OpcionLista(jugador.Nombre, jugador.Nombre));
            }

            _cmbValor.Items.Add(new OpcionLista("Banco", Transaccion.Banco));
        }
        else if (filtro == FiltroTransacciones.Tipo)
        {
            foreach (TipoTransaccion tipo in Enum.GetValues<TipoTransaccion>())
            {
                _cmbValor.Items.Add(new OpcionLista(Formato.Nombre(tipo), tipo.ToString()));
            }
        }

        _cmbValor.Visible = _cmbValor.Items.Count > 0;
        if (_cmbValor.Visible)
        {
            // Por jugador, empieza por el propio.
            int inicial = 0;
            for (int i = 0; i < _cmbValor.Items.Count; i++)
            {
                if (((OpcionLista)_cmbValor.Items[i]!).Valor == _sesion.Nombre)
                {
                    inicial = i;
                }
            }

            _cargandoValores = true;
            _cmbValor.SelectedIndex = inicial;
            _cargandoValores = false;
        }

        _cmbValor.EndUpdate();
    }

    private void AlCambiarValor(object? remitente, EventArgs e)
    {
        if (!_cargandoValores)
        {
            Consultar();
        }
    }

    private void Consultar()
    {
        FiltroTransacciones filtro = Filtros[_filtro];
        string? valor = _cmbValor.Visible && _cmbValor.SelectedItem is OpcionLista opcion ? opcion.Valor : null;
        if ((filtro == FiltroTransacciones.Jugador || filtro == FiltroTransacciones.Tipo) && valor == null)
        {
            return;
        }

        _esperandoConsulta = true;
        _lblResultado.ForeColor = Tema.TintaSuave;
        _lblResultado.Text = "Consultando al servidor...";
        _sesion.Solicitar(cliente => cliente.ConsultarTransacciones(filtro, valor));
    }

    private void Exportar()
    {
        _esperandoExportacion = true;
        _lblResultado.ForeColor = Tema.TintaSuave;
        _lblResultado.Text = "Solicitando la exportación al servidor...";
        _sesion.Solicitar(cliente => cliente.ExportarTransacciones());
    }

    private void AlActualizarEstado(EstadoRed estado)
    {
        // Llegaron transacciones nuevas: se repite la consulta con el mismo filtro.
        int cantidad = estado.Instantanea.CantidadTransacciones;
        if (cantidad != _ultimaCantidad)
        {
            _ultimaCantidad = cantidad;
            Consultar();
        }
    }

    private void AlRecibirTransacciones(string filtro, ListaSimple<Transaccion> transacciones)
    {
        _esperandoConsulta = false;
        string? referencia = Filtros[_filtro] == FiltroTransacciones.Jugador && _cmbValor.SelectedItem is OpcionLista opcion && opcion.Valor != Transaccion.Banco
            ? opcion.Valor
            : _sesion.Nombre;
        _tabla.Mostrar(transacciones, referencia);
        _lblResultado.ForeColor = Tema.Tinta;
        string valor = _cmbValor.Visible && _cmbValor.SelectedItem is OpcionLista elegido ? $": {elegido}" : string.Empty;
        _lblResultado.Text = $"{transacciones.Cantidad} transacción(es) · {NombresFiltro[_filtro]}{valor} · montos en verde: ingresos de {referencia}; en rojo: pagos.";
    }

    private void AlRecibirEvento(string texto)
    {
        if (!_esperandoExportacion || !texto.StartsWith(PrefijoExportado, StringComparison.Ordinal))
        {
            return;
        }

        _esperandoExportacion = false;
        _rutaExportada = texto.Substring(PrefijoExportado.Length).TrimEnd('.');
        _lblResultado.ForeColor = Tema.Exito;
        _lblResultado.Text = _sesion.EsOrganizador
            ? $"TXT generado en esta computadora: {_rutaExportada}"
            : $"TXT generado en la computadora del organizador (banco): {_rutaExportada}";
        _notificaciones.Mostrar("Historial exportado a TXT.", TipoNotificacion.Exito);
        _btnAbrirCarpeta.Visible = _sesion.EsOrganizador;
    }

    private void AlRecibirError(string mensaje)
    {
        if (!_esperandoExportacion && !_esperandoConsulta)
        {
            return;
        }

        _esperandoExportacion = false;
        _esperandoConsulta = false;
        _lblResultado.ForeColor = Tema.Error;
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
