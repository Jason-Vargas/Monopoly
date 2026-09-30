using System;
using System.Threading;
using Monopoly.Core.Estructuras;
using Monopoly.Core.Red;
using Xunit;

namespace Monopoly.Tests.Red;

/// <summary>
/// Cliente real conectado al servidor que guarda todos los mensajes recibidos y permite esperar,
/// con límite de tiempo, el siguiente que cumpla una condición.
/// </summary>
internal sealed class ClientePrueba : IDisposable
{
    private const int EsperaMaximaMs = 5000;

    private readonly object _candado = new object();
    private readonly ListaSimple<Mensaje> _recibidos = new ListaSimple<Mensaje>();
    private int _cursor;

    public ClientePrueba(int puerto, string nombre)
    {
        Nombre = nombre;
        Cliente.MensajeRecibido += mensaje =>
        {
            lock (_candado)
            {
                _recibidos.AgregarAlFinal(mensaje);
                Monitor.PulseAll(_candado);
            }
        };
        Cliente.Conectar("127.0.0.1", puerto);
    }

    public Cliente Cliente { get; } = new Cliente();

    public string Nombre { get; }

    /// <summary>
    /// Envía CONECTAR con su nombre y espera la BIENVENIDA.
    /// </summary>
    public int Unirse()
    {
        Cliente.Unirse(Nombre);
        return Esperar(Protocolo.Bienvenida).Entero(0);
    }

    /// <summary>
    /// Espera el siguiente mensaje (a partir del último devuelto) con ese comando y condición.
    /// Los mensajes anteriores al encontrado se dan por leídos.
    /// </summary>
    public Mensaje Esperar(string comando, Predicate<Mensaje>? condicion = null)
    {
        DateTime limite = DateTime.UtcNow.AddMilliseconds(EsperaMaximaMs);
        lock (_candado)
        {
            int revisado = _cursor;
            while (true)
            {
                for (; revisado < _recibidos.Cantidad; revisado++)
                {
                    Mensaje mensaje = _recibidos.Obtener(revisado);
                    if (mensaje.Comando == comando && (condicion == null || condicion(mensaje)))
                    {
                        _cursor = revisado + 1;
                        return mensaje;
                    }
                }

                int restante = (int)(limite - DateTime.UtcNow).TotalMilliseconds;
                if (restante <= 0)
                {
                    throw new Xunit.Sdk.XunitException(
                        $"{Nombre}: no llegó ningún {comando} esperado en {EsperaMaximaMs} ms. Recibidos: {Resumen()}");
                }

                Monitor.Wait(_candado, restante);
            }
        }
    }

    /// <summary>
    /// Espera el siguiente ESTADO que cumpla la condición.
    /// </summary>
    public EstadoRed EsperarEstado(Predicate<EstadoRed> condicion)
    {
        return SerializadorEstado.Decodificar(Esperar(Protocolo.Estado, m => condicion(SerializadorEstado.Decodificar(m))));
    }

    /// <summary>
    /// Espera un ERROR que contenga el texto indicado.
    /// </summary>
    public void EsperarError(string texto)
    {
        Mensaje error = Esperar(Protocolo.Error, m => m.Campo(0).Contains(texto, StringComparison.Ordinal));
        Assert.Contains(texto, error.Campo(0));
    }

    public void Dispose()
    {
        Cliente.Dispose();
    }

    private string Resumen()
    {
        string texto = "";
        _recibidos.Recorrer(m =>
        {
            string linea = m.ToString();
            texto += "\n  " + (linea.Length > 120 ? linea.Substring(0, 120) + "…" : linea);
        });
        return texto;
    }
}
