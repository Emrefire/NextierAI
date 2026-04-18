using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using NexTierAI.Application.Services;
using NexTierAI.Domain.Interfaces;
using NexTierAI.Infrastructure.Services;
using System;

namespace NexTierAI.UI;

public partial class App : Microsoft.UI.Xaml.Application
{
    // Sistemin her yerinden servislerimize ulaşmamızı sağlayacak merkez
    public IServiceProvider Services { get; }

    public App()
    {
        this.InitializeComponent();

        // 1. Servis Koleksiyonunu Oluştur
        var services = new ServiceCollection();

        // 2. Servislerimizi Sisteme Kaydediyoruz (Orkestra burada birleşiyor)
        // Singleton: Uygulama açık kaldığı sürece sadece 1 tane üretilir (Hafıza dostu)
        services.AddSingleton<ILlmService, OllamaService>();
        services.AddSingleton<IVectorDbService, LocalVectorDbService>();

        // Transient: Her çağrıldığında yeni bir kopyası üretilir (Orkestratör için ideal)
        services.AddTransient<MentorOrchestrator>();

        // 3. Koleksiyonu İnşa Et
        Services = services.BuildServiceProvider();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        m_window = new MainWindow();
        m_window.Activate();
    }

    private Window? m_window;
}