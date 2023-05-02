using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;



namespace Shared.Services;

public class ConfigData
{
	public string LocalDatabaseConnectionString { get; internal set; } = string.Empty;//Use this One because it can be either pension or Insurance
	public string EiopaDatabaseConnectionString { get; internal set; } = string.Empty; //Use this One because it depends on solvency version

	public string BackendDatabaseConnectionString { get; set; } = string.Empty;
	public string ExcelArchiveDatabaseConnectionString { get; set; } = string.Empty;


	public string OutputXbrlFolder { get; set; } = string.Empty;

	public string ExcelTemplateFileGeneral { get; internal set; } = string.Empty;

	public string LoggerXbrlReaderFile { get; set; } = string.Empty;
	public string LoggerExcelWriterFile { get; set; } = string.Empty;
	public string LoggerExcelReaderFile { get; set; } = string.Empty;
	public string LoggerValidatorFile { get; set; } = string.Empty;
	public string LoggerXbrlFile { get; set; } = string.Empty;
	public string LoggerAggregatorFile { get; set; } = string.Empty;

}

public class ConfigObject : IConfigObject
{
	//New version to read json	
	//by passing a the solvency version as a param, the user can select which database version is selected as LocalDatabaseConnection

	readonly string _filename;
	public ConfigData Data { get; private set; } = new ConfigData();
	public string Version { get; } = string.Empty;

	public static bool IsValidVersion(string version)
	{

		var validValues = new List<string>() { "PP250", "PU250", "PU270", "IU250", "IU260", "IU270", "TEST250" };
		var isValid = validValues.Contains(version);
		return isValid;
	}
	public ConfigObject(string version)
	{
		Version = version;


#if DEBUG
		Console.WriteLine("Mode=Debug");
        //
        //_filename = @"C:\Users\kyrlo\soft\dotnet\pension-project\Pension_dev_NEW\ConfigDataNd.json";
        
        _filename = @"C:\Users\kyrlo\soft\Executables\Insurance\x1\ConfigDataNew.json";
#else
            Console.WriteLine("Mode=Release from Config");
            try
            {
                _filename = Path.Combine(Directory.GetCurrentDirectory(), "ConfigDataNew.json");                               
                Console.WriteLine($"Config: {_filename}");
            }
            catch (Exception e)
            {
                var message = $"Error reading ConfigDataNew.json from current directory ";
                Console.WriteLine(message);
                Console.WriteLine(e);
                throw new Exception(message);
            }
#endif

        var jsonDataString = string.Empty;
		try
		{
			jsonDataString = File.ReadAllText(_filename);
		}
		catch (Exception e)
		{
			var message = $"Error Reading file:{_filename}-exception {e.Message} ";
			Console.WriteLine(message);
			throw new Exception(message);
		}

		var options = new JsonSerializerOptions
		{
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true,
		};
		JsonDataClass jsonData;
		try
		{
			jsonData = JsonSerializer.Deserialize<JsonDataClass>(jsonDataString, options) ?? new JsonDataClass();
		}
		catch (Exception e)
		{
			var justFileName = Path.GetFileName(_filename);
			var message = $" Cannot read Json file:{justFileName} /n--{e.Message} ";
			Console.WriteLine(message);
			throw new Exception(message);
		}


		var versionData = jsonData?.VersionData?.FirstOrDefault(item => item.version == Version);
		if (versionData is null)
		{
			var message = $"Cannot find item for version : {Version} ";
			Console.WriteLine(message);
			throw new Exception(message);
		}

		Data.BackendDatabaseConnectionString = jsonData?.BackendDatabaseConnectionString ?? string.Empty;
		Data.ExcelArchiveDatabaseConnectionString = jsonData?.ExcelArchiveDatabaseConnectionString ?? string.Empty;
		Data.OutputXbrlFolder = jsonData?.OutputXbrlFolder ?? string.Empty;
		Data.LocalDatabaseConnectionString = versionData.SystemDatabaseString ?? string.Empty;
		Data.EiopaDatabaseConnectionString = versionData.EiopaConnectionString ?? string.Empty;
		Data.ExcelTemplateFileGeneral = versionData.ExcelTemplateFile ?? string.Empty;

		Data.LoggerXbrlFile = jsonData?.LoggerFiles?.LoggerXbrlFile ?? string.Empty;
		Data.LoggerXbrlReaderFile = jsonData?.LoggerFiles?.LoggerXbrlReaderFile ?? string.Empty;
		Data.LoggerValidatorFile = jsonData?.LoggerFiles?.LoggerValidatorFile ?? string.Empty;
		Data.LoggerExcelReaderFile = jsonData?.LoggerFiles?.LoggerExcelReaderFile ?? string.Empty;
		Data.LoggerExcelWriterFile = jsonData?.LoggerFiles?.LoggerExcelWriterFile ?? string.Empty;
		Data.LoggerAggregatorFile = jsonData?.LoggerFiles?.LoggerAggregatorFile ?? string.Empty;

	}
	public ConfigData GetInstance(string version)
	{
		return Data;
	}
}

