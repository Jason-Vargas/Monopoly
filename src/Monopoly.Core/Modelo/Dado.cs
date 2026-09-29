using System;

namespace Monopoly.Core.Modelo;

/// <summary>
/// Par de dados electrónicos (modo simulado). Genera dos valores del 1 al 6, de forma aleatoria,
/// con una semilla reproducible o a partir de una secuencia de valores fijos para pruebas.
/// </summary>
public class Dado
{
    /// <summary>
    /// Valor mínimo de un dado.
    /// </summary>
    public const int Minimo = 1;

    /// <summary>
    /// Valor máximo de un dado.
    /// </summary>
    public const int Maximo = 6;

    private readonly Random? _aleatorio;
    private readonly int[]? _valoresFijos;
    private int _siguienteFijo;

    /// <summary>
    /// Crea dados aleatorios.
    /// </summary>
    public Dado()
        : this(new Random())
    {
    }

    /// <summary>
    /// Crea dados aleatorios con una semilla fija (misma semilla, misma secuencia).
    /// </summary>
    /// <param name="semilla">Semilla del generador.</param>
    public Dado(int semilla)
        : this(new Random(semilla))
    {
    }

    /// <summary>
    /// Crea dados que usan el generador indicado.
    /// </summary>
    /// <param name="aleatorio">Generador de números aleatorios.</param>
    public Dado(Random aleatorio)
    {
        ArgumentNullException.ThrowIfNull(aleatorio);
        _aleatorio = aleatorio;
    }

    private Dado(int[] valoresFijos)
    {
        _valoresFijos = valoresFijos;
    }

    /// <summary>
    /// Última tirada realizada, o <c>null</c> si aún no se ha lanzado.
    /// </summary>
    public TiradaDados? UltimaTirada { get; private set; }

    /// <summary>
    /// Crea dados que devuelven los valores indicados en orden, uno por dado, y vuelven a empezar
    /// al agotarlos. Por ejemplo, (3, 4, 6, 6) produce las tiradas 3+4 y 6+6.
    /// </summary>
    /// <param name="valores">Valores entre 1 y 6 (al menos uno).</param>
    /// <returns>Dados deterministas.</returns>
    /// <exception cref="ArgumentException">Si no hay valores.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si algún valor no está entre 1 y 6.</exception>
    public static Dado ConValoresFijos(params int[] valores)
    {
        ArgumentNullException.ThrowIfNull(valores);
        if (valores.Length == 0)
        {
            throw new ArgumentException("Debe indicar al menos un valor.", nameof(valores));
        }

        int[] copia = new int[valores.Length];
        for (int i = 0; i < valores.Length; i++)
        {
            if (valores[i] < Minimo || valores[i] > Maximo)
            {
                throw new ArgumentOutOfRangeException(nameof(valores), $"Los valores deben estar entre {Minimo} y {Maximo}.");
            }

            copia[i] = valores[i];
        }

        return new Dado(copia);
    }

    /// <summary>
    /// Lanza los dos dados.
    /// </summary>
    /// <returns>La tirada obtenida.</returns>
    public TiradaDados Lanzar()
    {
        TiradaDados tirada = new TiradaDados(SiguienteValor(), SiguienteValor());
        UltimaTirada = tirada;
        return tirada;
    }

    private int SiguienteValor()
    {
        if (_valoresFijos != null)
        {
            int valor = _valoresFijos[_siguienteFijo];
            _siguienteFijo = (_siguienteFijo + 1) % _valoresFijos.Length;
            return valor;
        }

        return _aleatorio!.Next(Minimo, Maximo + 1);
    }
}
