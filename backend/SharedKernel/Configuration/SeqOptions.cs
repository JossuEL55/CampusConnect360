namespace SharedKernel.Configuration;

public sealed class SeqOptions
{
    public const string SectionName = "Seq";

    public string ServerUrl { get; init; } = "http://localhost:5341";
}