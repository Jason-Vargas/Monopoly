using System.Drawing;
using System.Windows.Forms;
using Monopoly.Core.Logica;
using Monopoly.Core.Modelo;
using Monopoly.Core.Red;

namespace Monopoly.App;

/// <summary>
/// Diálogo de fin de partida: ganador y patrimonio de cada jugador, de mayor a menor.
/// </summary>
internal sealed class FormularioFin : Form
{
    /// <summary>
    /// Crea el diálogo.
    /// </summary>
    /// <param name="estado">Último estado recibido (para la tabla de patrimonios).</param>
    /// <param name="ganador">Nombre del ganador.</param>
    /// <param name="resumen">Resumen enviado por el servidor.</param>
    /// <param name="miId">Id del jugador de esta ventana.</param>
    public FormularioFin(EstadoRed? estado, string ganador, string resumen, int? miId)
    {
        Text = "Fin de la partida";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(660, 440);
        BackColor = Paleta.FondoTablero;
        Font = new Font(Paleta.Fuente, 10f);

        bool gane = estado?.BuscarJugador(miId ?? 0)?.Nombre == ganador;
        Label titulo = new Label
        {
            Text = gane ? "¡Usted ganó la partida!" : $"Ganador: {ganador}",
            Font = new Font("Georgia", 20f, FontStyle.Bold),
            ForeColor = Paleta.RojoTitulo,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 70,
        };

        ListView tabla = new ListView { View = View.Details, FullRowSelect = true, GridLines = true, Dock = DockStyle.Fill, Font = new Font(Paleta.Fuente, 10.5f) };
        tabla.Columns.Add("Puesto", 70, HorizontalAlignment.Center);
        tabla.Columns.Add("Jugador", 140);
        tabla.Columns.Add("Patrimonio", 115, HorizontalAlignment.Right);
        tabla.Columns.Add("Saldo", 90, HorizontalAlignment.Right);
        tabla.Columns.Add("Propiedades", 110, HorizontalAlignment.Right);
        tabla.Columns.Add("Estado", 95);

        EstadoJugador[] ordenados = Ordenar(estado?.Instantanea.Jugadores ?? new EstadoJugador[0], estado?.Instantanea.IdGanador);
        for (int i = 0; i < ordenados.Length; i++)
        {
            EstadoJugador j = ordenados[i];
            ListViewItem fila = new ListViewItem((i + 1).ToString());
            fila.SubItems.Add(j.Nombre);
            fila.SubItems.Add(Formato.Dinero(j.Patrimonio));
            fila.SubItems.Add(Formato.Dinero(j.Saldo));
            fila.SubItems.Add(j.IdsPropiedades.Length.ToString());
            fila.SubItems.Add(j.Activo ? "Activo" : "Eliminado");
            if (j.Nombre == ganador)
            {
                fila.Font = new Font(tabla.Font, FontStyle.Bold);
                fila.BackColor = Color.FromArgb(255, 240, 180);
            }

            tabla.Items.Add(fila);
        }

        TextBox detalle = new TextBox { Text = resumen, ReadOnly = true, Multiline = true, Dock = DockStyle.Bottom, Height = 84, ScrollBars = ScrollBars.Vertical };
        Button aceptar = new Button { Text = "Aceptar", Dock = DockStyle.Bottom, Height = 40, DialogResult = DialogResult.OK };
        AcceptButton = aceptar;

        Controls.Add(tabla);
        Controls.Add(detalle);
        Controls.Add(aceptar);
        Controls.Add(titulo);
    }

    /// <summary>
    /// Ordena una copia de los jugadores (inserción): primero el ganador (que puede haber ganado un
    /// empate de patrimonio por el orden de turnos) y luego por patrimonio, de mayor a menor.
    /// </summary>
    private static EstadoJugador[] Ordenar(EstadoJugador[] jugadores, int? idGanador)
    {
        EstadoJugador[] copia = new EstadoJugador[jugadores.Length];
        for (int i = 0; i < jugadores.Length; i++)
        {
            EstadoJugador actual = jugadores[i];
            int j = i - 1;
            while (j >= 0 && VaAntes(actual, copia[j], idGanador))
            {
                copia[j + 1] = copia[j];
                j--;
            }

            copia[j + 1] = actual;
        }

        return copia;
    }

    private static bool VaAntes(EstadoJugador a, EstadoJugador b, int? idGanador)
    {
        if (a.Id == idGanador || b.Id == idGanador)
        {
            return a.Id == idGanador;
        }

        return a.Patrimonio > b.Patrimonio;
    }
}
