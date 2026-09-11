using System.Text.Json;
using CasaMiga.Application;

namespace CasaMiga.Infrastructure;

public sealed class PrintingConfigurationProvider(string dataDirectory) : IPrintingSettingsProvider
{
    public const string FileName = "printing.json";
    private readonly string path = Path.Combine(dataDirectory,FileName);
    private static readonly JsonSerializerOptions Options=new(){PropertyNameCaseInsensitive=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase};

    public PrintingSettingsResult Read()
    {
        if(!File.Exists(path))return new(null,"CONFIG_MISSING");
        try
        {
            var settings=JsonSerializer.Deserialize<PrintingSettings>(File.ReadAllText(path),Options);
            if(settings is null || settings.Business is null || settings.Version!=1 || settings.Columns is <32 or >64
                || string.IsNullOrWhiteSpace(settings.EncodingName) || settings.EncodingName.Length>100
                || !ValidEncodingSelection(settings.EncodingName,settings.CharacterTable)
                || settings.CutMode is not ("Disabled" or "Partial") || settings.OpenCashDrawer
                || (settings.Enabled && (string.IsNullOrWhiteSpace(settings.QueueName) || string.IsNullOrWhiteSpace(settings.Business.DisplayName)
                    || settings.QueueName.Length>260 || settings.Business.DisplayName.Length>120
                    || string.IsNullOrWhiteSpace(settings.Business.ClosingMessage) || settings.Business.ClosingMessage.Length>500
                    || new[]{settings.Business.Address,settings.Business.Phone,settings.Business.TaxId,settings.Business.Contact,settings.Business.MonochromeLogoPath}
                        .Any(value=>value?.Length>500))))return new(null,"CONFIG_INVALID");
            return settings.Enabled?new(settings with{QueueName=settings.QueueName.Trim()},null):new(settings,"PRINTING_DISABLED");
        }
        catch(Exception error) when(error is JsonException or IOException or UnauthorizedAccessException)
        {
            return new(null,"CONFIG_INVALID");
        }
    }

    private static bool ValidEncodingSelection(string encodingName,int? characterTable)
        =>(encodingName.ToLowerInvariant(),characterTable) switch
        {
            ("us-ascii",null)=>true,
            ("ibm850",2)=>true,
            ("windows-1252",16)=>true,
            _=>false
        };
}
