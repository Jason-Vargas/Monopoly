using System.IO;
using Monopoly.Core.Estructuras;
using Monopoly.Core.Hardware;

namespace Monopoly.Tests.Hardware;

/// <summary>
/// Cajero de prueba: usa el mismo intérprete de líneas que la Pico real, pero las líneas se inyectan
/// desde la prueba y las que el servidor envía quedan guardadas.
/// </summary>
internal sealed class CajeroFalso : CajeroPorLineas
{
    private readonly object _candado = new object();
    private readonly ListaSimple<string> _enviadas = new ListaSimple<string>();

    public bool FallarAlEnviar { get; set; }

    public override string Descripcion => "Cajero falso de pruebas";

    public void Conectar() => CambiarEstado(EstadoCajero.Conectado);

    /// <summary>
    /// Simula que se desconecta el cable.
    /// </summary>
    public void Desenchufar() => CambiarEstado(EstadoCajero.Desconectado);

    /// <summary>
    /// Simula una línea recibida por el puerto serie.
    /// </summary>
    public void Inyectar(string linea) => ProcesarLinea(linea);

    public string[] Enviadas()
    {
        lock (_candado)
        {
            string[] copia = new string[_enviadas.Cantidad];
            int i = 0;
            _enviadas.Recorrer(linea => copia[i++] = linea);
            return copia;
        }
    }

    protected override void EnviarLinea(string linea)
    {
        if (FallarAlEnviar)
        {
            throw new IOException("El dispositivo no existe.");
        }

        lock (_candado)
        {
            _enviadas.AgregarAlFinal(linea);
        }
    }
}
