namespace Shared.Services;
public interface IConfigObject
{
	ConfigData Data { get; }
	string Version { get; }

	ConfigData GetInstance(string version);
}
