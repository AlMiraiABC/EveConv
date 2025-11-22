using System;
using System.Collections.Generic;
using System.Text;
using EveConv.Abstraction;
using Microsoft.Extensions.DependencyInjection;

namespace EveConv.MimeType
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddMimeTypeDetection(this IServiceCollection services)
        {
            services.AddSingleton<IMimeTypeDetection, MimeTypesDetection>();
            return services;
        }
    }
}
