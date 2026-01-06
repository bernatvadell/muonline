using Client.Main.Scenes;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Main
{
    public class GameBootstrap
    {
        private readonly ServiceCollection _services;

        public ServiceCollection Services => _services;

        public GameBootstrap()
        {
            _services = new ServiceCollection();

            RegisterInternalServices();
        }

        private void RegisterInternalServices()
        {
            _services.AddSingleton<MuGame>();
            _services.AddTransient<LoadScene>();
            _services.AddTransient<LoginScene>();
            _services.AddTransient<SelectCharacterScene>();
            _services.AddTransient<GameScene>();
        }

        public IServiceProvider Build()
        {
            return _services.BuildServiceProvider(
                new ServiceProviderOptions
                {
                    ValidateScopes = true,
                    ValidateOnBuild = true
                });
        }
    }
}
