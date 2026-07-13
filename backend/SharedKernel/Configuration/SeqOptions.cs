namespace SharedKernel.Configuration;

// Define la configuración utilizada para enviar logs estructurados al servidor Seq.
public sealed class SeqOptions
{
    public const string SectionName = "Seq";

    public string ServerUrl { get; init; } = "http://localhost:5341";
}