namespace Alerta.Core;

public sealed record Exercise(string Title, string Instructions)
{
    /// <summary>Card shown when health tips are switched off.</summary>
    public static Exercise PauseOnly { get; } = new("Faça uma pausa", "");
}

public sealed class ExerciseCatalog
{
    public static IReadOnlyList<Exercise> Default { get; } =
    [
        new("Regra 20-20-20", "Olhe para algo a pelo menos 6 metros de distância por 20 segundos. Pisque devagar algumas vezes."),
        new("Alongue o pescoço", "Incline a cabeça para a direita por 15 s, depois para a esquerda. Termine com o queixo no peito por 15 s."),
        new("Solte os ombros", "Gire os ombros para trás 10 vezes e para frente 10 vezes. Depois aperte as escápulas por 5 s."),
        new("Punhos e dedos", "Estique o braço com a palma para cima e puxe os dedos para baixo com a outra mão por 15 s. Troque de lado."),
        new("Levante e caminhe", "Fique de pé e dê uma volta de 2 minutos. Vale ir até a janela ou buscar um café."),
        new("Beba água", "Levante, encha o copo e beba devagar. Hidratação também ajuda a manter o foco."),
        new("Respiração 4-7-8", "Inspire pelo nariz em 4 s, segure por 7 s e solte pela boca em 8 s. Repita 4 vezes."),
        new("Alongue as costas", "De pé, entrelace os dedos acima da cabeça e estique o corpo para cima por 20 s. Depois incline para cada lado."),
    ];

    private readonly IReadOnlyList<Exercise> _items;
    private readonly Random _random;
    private int _last = -1;

    public ExerciseCatalog(IReadOnlyList<Exercise>? items = null, Random? random = null)
    {
        _items = items ?? Default;
        if (_items.Count == 0) throw new ArgumentException("Catalog needs at least one exercise.", nameof(items));
        _random = random ?? Random.Shared;
    }

    public Exercise NextFor(Settings settings) => settings.ShowHealthTips ? Next() : Exercise.PauseOnly;

    public Exercise Next()
    {
        if (_items.Count == 1) return _items[0];

        int index;
        do index = _random.Next(_items.Count);
        while (index == _last);

        _last = index;
        return _items[index];
    }
}
