using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Shared.Services
{
	public class HostCreator
	{
		public static IConfigObject CreateTheHost(string solvencyVersion)
		{
			using IHost host = Host.CreateDefaultBuilder()
			.ConfigureServices(services => services.AddSingleton(new ConfigObject(solvencyVersion)))
			.Build();

			IConfigObject config = host.Services.GetRequiredService<ConfigObject>();
			return config;

		}
	}
}
