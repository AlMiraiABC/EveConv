namespace EveConv.HuggingFace;

public record Config
{
    public string AuthorizationToken { get; set; } = string.Empty;

    public string Endpoint
    {
        get => string.IsNullOrWhiteSpace(field) ? "https://huggingface.co" : field;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }
            value = value.Trim();
            if (value.EndsWith('/'))
            {
                value = value[..^1];
            }
            field = value;
        }
    }

    public string Proxy { get; set; } = string.Empty;
    public IDictionary<string, string> ExtraHeaders { get; set; } = new Dictionary<string, string>();
}
