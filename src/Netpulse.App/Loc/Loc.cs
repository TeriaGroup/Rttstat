using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace Netpulse.App.Loc;

public sealed class LExtension : MarkupExtension
{
    public LExtension() { }
    public LExtension(string key) => Key = key;
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding(nameof(I18n.Language))
        {
            Source = I18n.Current,
            Mode = BindingMode.OneWay,
            Converter = LocConverter.Instance,
            ConverterParameter = Key
        };
        if (serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget t
            && t.TargetObject is DependencyObject)
            return binding.ProvideValue(serviceProvider);
        return I18n.Current[Key];
    }
}

public sealed class PingStatusConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? parameter, CultureInfo culture)
    {
        var s = value as string ?? "";
        var mapped = I18n.Current["st." + s];
        return mapped.StartsWith("st.", StringComparison.Ordinal) ? s : mapped;
    }
    public object ConvertBack(object? value, Type t, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

internal sealed class LocConverter : IValueConverter
{
    public static LocConverter Instance { get; } = new();
    public object Convert(object? value, Type t, object? parameter, CultureInfo culture)
        => I18n.Current[parameter as string ?? ""];
    public object ConvertBack(object? value, Type t, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class I18n : INotifyPropertyChanged
{
    public static I18n Current { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Language { get; private set; } = "ru";

    public string this[string key] => Get(key);

    public IReadOnlyList<string> Languages { get; } = ["ru", "en"];

    public void SetLanguage(string lang)
    {
        Language = lang is "en" ? "en" : "ru";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public string Get(string key)
    {
        var map = Language == "en" ? En : Ru;
        if (map.TryGetValue(key, out var s)) return s;
        if (En.TryGetValue(key, out var en)) return en;
        return key;
    }

    private static readonly Dictionary<string, string> Ru = new()
    {
        ["pause"] = "Пауза",
        ["resume"] = "Продолжить",
        ["speedtest"] = "Замер скорости",
        ["nav.dash"] = "Обзор",
        ["nav.targets"] = "Цели",
        ["nav.log"] = "Журнал",
        ["nav.stats"] = "Статистика",
        ["nav.speed"] = "Скорость",
        ["nav.settings"] = "Настройки",
        ["ping"] = "Пинг",
        ["loss"] = "Потери пакетов",
        ["outage"] = "Потеря связи",
        ["throughput"] = "Трафик",
        ["link"] = "Канал",
        ["down.mbps"] = "Загрузка, Мбит/с",
        ["col.name"] = "Имя",
        ["col.host"] = "Хост",
        ["col.ip"] = "IP",
        ["col.rtt"] = "RTT",
        ["col.status"] = "Статус",
        ["col.loss5"] = "Потери 5м",
        ["activate"] = "Включить",
        ["save"] = "Сохранить",
        ["duplicate"] = "Копия",
        ["delete"] = "Удалить",
        ["detect.gw"] = "Найти шлюз",
        ["add.target"] = "Добавить цель",
        ["filter"] = "Фильтр",
        ["export.csv"] = "Экспорт CSV",
        ["refresh"] = "Обновить",
        ["export"] = "Экспорт",
        ["speed.title"] = "Замер канала (LibreSpeed / HTTP)",
        ["run.test"] = "Запустить",
        ["cancel"] = "Отмена",
        ["col.when"] = "Когда",
        ["col.down"] = "Вниз",
        ["col.up"] = "Вверх",
        ["col.server"] = "Сервер",
        ["col.error"] = "Ошибка",
        ["col.time"] = "Время",
        ["col.level"] = "Уровень",
        ["col.cat"] = "Категория",
        ["col.msg"] = "Сообщение",
        ["col.on"] = "Вкл",
        ["col.role"] = "Роль",
        ["set.general"] = "Общее",
        ["set.lang"] = "Язык",
        ["set.autostart"] = "Запускать с Windows",
        ["set.open"] = "Открывать окно при старте",
        ["set.mute"] = "Отключить все уведомления",
        ["set.toast.outage"] = "Уведомлять об обрывах",
        ["set.toast.th"] = "Уведомлять о порогах пинга/потерь",
        ["set.toast.speed"] = "Уведомлять о замере скорости",
        ["set.quiet"] = "Тихие часы",
        ["set.to"] = "до",
        ["set.adapter"] = "Адаптер",
        ["set.tray"] = "Трей и HUD",
        ["set.hud"] = "HUD у трея",
        ["set.strip"] = "Полоска-график",
        ["set.flyout"] = "Всплывающая панель",
        ["set.hidefs"] = "Скрывать HUD в полноэкранных играх",
        ["set.schedule"] = "Расписание замера (Выкл, каждые N ч или ЧЧ:мм)",
        ["set.metered"] = "Разрешить замер на лимитном интернете",
        ["set.ret"] = "Хранение, дни (сырые / минуты / часы / дни / события)",
        ["set.save"] = "Сохранить настройки",
        ["set.folder"] = "Папка данных",
        ["set.vacuum"] = "Сжать базу",
        ["set.reset"] = "Сбросить настройки",
        ["set.about"] = "Rttstat 0.1.0 — ICMP может быть запрещён политикой, тогда используется TCP. Данные только на этом ПК.",
        ["open"] = "Открыть",
        ["exit"] = "Выход",
        ["tray.open"] = "Открыть Rttstat",
        ["tray.pause"] = "Пауза мониторинга",
        ["tray.resume"] = "Возобновить мониторинг",
        ["tray.speed"] = "Замер скорости",
        ["tray.profiles"] = "Профили",
        ["tray.hud"] = "HUD включён",
        ["tray.strip"] = "График включён",
        ["paused"] = "Пауза",
        ["adapter_down"] = "Адаптер выключен",
        ["down"] = "ОБРЫВ",
        ["no_internet"] = "Нет интернета",
        ["gateway_warn"] = "Шлюз недоступен, интернет есть",
        ["high_latency"] = "Высокий пинг или потери",
        ["degraded"] = "Канал хуже обычного",
        ["online"] = "Онлайн",
        ["q.Ok"] = "Норма",
        ["q.Warn"] = "Внимание",
        ["q.Bad"] = "Плохо",
        ["q.Down"] = "Обрыв",
        ["q.Paused"] = "Пауза",
        ["online.for"] = "Онлайн {0}",
        ["outage.for"] = "Обрыв {0}",
        ["bucket.Hour"] = "Час",
        ["bucket.Day"] = "День",
        ["bucket.Week"] = "Неделя",
        ["toast.outage"] = "Rttstat — обрыв",
        ["toast.up"] = "Rttstat — связь восстановлена",
        ["toast.dur"] = "Длительность {0}",
        ["toast.speed.ok"] = "Замер скорости",
        ["toast.speed.fail"] = "Замер не удался",
        ["msg.speed.warn"] = "Замер займёт канал примерно на 20 секунд. Продолжить?",
        ["msg.db"] = "База очищена.",
        ["msg.reset"] = "Настройки сброшены. Перезапустите Rttstat.",
        ["msg.saved"] = "Настройки сохранены",
        ["up"] = "Отдача",
        ["dn"] = "Загрузка",
        ["lang.ru"] = "Русский",
        ["lang.en"] = "English",
        ["stats.ping"] = "Пинг ср. {0}  мин {1}  макс {2} мс",
        ["stats.loss"] = "Потери {0}%   успешно {1} / сбой {2}",
        ["stats.outages"] = "Обрывы {0}   всего {1}   самый длинный {2}",
        ["stats.traffic"] = "Трафик загрузка {0} МБ  отдача {1} МБ",
        ["stats.speed"] = "Замеры {0}  лучшие ↓ {1}  ↑ {2} Мбит/с",
        ["st.ok"] = "ок",
        ["st.tcp"] = "tcp",
        ["st.tcp-timeout"] = "таймаут tcp",
        ["st.tcp-refused"] = "tcp отказ",
        ["sched.off"] = "Выкл",
        ["cat.All"] = "Все",
        ["cat.App"] = "Приложение",
        ["cat.Outage"] = "Обрыв",
        ["cat.Ping"] = "Пинг",
        ["cat.Threshold"] = "Порог",
        ["cat.Adapter"] = "Адаптер",
        ["cat.Speedtest"] = "Скорость",
        ["cat.Profile"] = "Профиль",
        ["cat.Settings"] = "Настройки",
    };

    private static readonly Dictionary<string, string> En = new()
    {
        ["pause"] = "Pause",
        ["resume"] = "Resume",
        ["speedtest"] = "Speed test",
        ["nav.dash"] = "Dashboard",
        ["nav.targets"] = "Targets",
        ["nav.log"] = "Log",
        ["nav.stats"] = "Statistics",
        ["nav.speed"] = "Speed tests",
        ["nav.settings"] = "Settings",
        ["ping"] = "Ping",
        ["loss"] = "Packet loss",
        ["outage"] = "Outage",
        ["throughput"] = "Throughput",
        ["link"] = "Link",
        ["down.mbps"] = "Download Mbps",
        ["col.name"] = "Name",
        ["col.host"] = "Host",
        ["col.ip"] = "IP",
        ["col.rtt"] = "RTT",
        ["col.status"] = "Status",
        ["col.loss5"] = "Loss 5m",
        ["activate"] = "Activate",
        ["save"] = "Save",
        ["duplicate"] = "Duplicate",
        ["delete"] = "Delete",
        ["detect.gw"] = "Detect gateway",
        ["add.target"] = "Add target",
        ["filter"] = "Filter",
        ["export.csv"] = "Export CSV",
        ["refresh"] = "Refresh",
        ["export"] = "Export",
        ["speed.title"] = "Path capacity test (LibreSpeed / HTTP)",
        ["run.test"] = "Run test",
        ["cancel"] = "Cancel",
        ["col.when"] = "When",
        ["col.down"] = "Down",
        ["col.up"] = "Up",
        ["col.server"] = "Server",
        ["col.error"] = "Error",
        ["col.time"] = "Time",
        ["col.level"] = "Level",
        ["col.cat"] = "Category",
        ["col.msg"] = "Message",
        ["col.on"] = "On",
        ["col.role"] = "Role",
        ["set.general"] = "General",
        ["set.lang"] = "Language",
        ["set.autostart"] = "Start with Windows",
        ["set.open"] = "Open window on start",
        ["set.mute"] = "Mute all notifications",
        ["set.toast.outage"] = "Toast on outage start/end",
        ["set.toast.th"] = "Toast on ping/loss thresholds",
        ["set.toast.speed"] = "Toast on speedtest complete",
        ["set.quiet"] = "Quiet hours",
        ["set.to"] = "to",
        ["set.adapter"] = "Adapter",
        ["set.tray"] = "Tray and HUD",
        ["set.hud"] = "HUD",
        ["set.strip"] = "Sparkline strip",
        ["set.flyout"] = "Flyout",
        ["set.hidefs"] = "Hide HUD in fullscreen",
        ["set.schedule"] = "Speedtest schedule (Off, Every N, or HH:mm)",
        ["set.metered"] = "Allow speedtest on metered connections",
        ["set.ret"] = "Retention days (raw / minute / hour / day / events)",
        ["set.save"] = "Save settings",
        ["set.folder"] = "Open data folder",
        ["set.vacuum"] = "Vacuum DB",
        ["set.reset"] = "Reset settings",
        ["set.about"] = "Rttstat 0.1.0 — ICMP may be blocked by policy; TCP fallback is automatic. Data stays on this PC.",
        ["open"] = "Open",
        ["exit"] = "Exit",
        ["tray.open"] = "Open Rttstat",
        ["tray.pause"] = "Pause monitoring",
        ["tray.resume"] = "Resume monitoring",
        ["tray.speed"] = "Run speed test",
        ["tray.profiles"] = "Profiles",
        ["tray.hud"] = "HUD enabled",
        ["tray.strip"] = "Strip enabled",
        ["paused"] = "Paused",
        ["adapter_down"] = "Adapter down",
        ["down"] = "DOWN",
        ["no_internet"] = "No internet",
        ["gateway_warn"] = "Gateway unreachable, internet OK",
        ["high_latency"] = "High latency or loss",
        ["degraded"] = "Degraded",
        ["online"] = "Online",
        ["q.Ok"] = "OK",
        ["q.Warn"] = "Warn",
        ["q.Bad"] = "Bad",
        ["q.Down"] = "Down",
        ["q.Paused"] = "Paused",
        ["online.for"] = "Online {0}",
        ["outage.for"] = "Outage {0}",
        ["bucket.Hour"] = "Hour",
        ["bucket.Day"] = "Day",
        ["bucket.Week"] = "Week",
        ["toast.outage"] = "Rttstat — outage",
        ["toast.up"] = "Rttstat — restored",
        ["toast.dur"] = "Duration {0}",
        ["toast.speed.ok"] = "Speed test",
        ["toast.speed.fail"] = "Speed test failed",
        ["msg.speed.warn"] = "This will use significant bandwidth for ~20 seconds. Continue?",
        ["msg.db"] = "Database cleaned.",
        ["msg.reset"] = "Settings reset. Restart Rttstat.",
        ["msg.saved"] = "Settings saved",
        ["up"] = "Upload",
        ["dn"] = "Download",
        ["lang.ru"] = "Русский",
        ["lang.en"] = "English",
        ["stats.ping"] = "Ping avg {0}  min {1}  max {2} ms",
        ["stats.loss"] = "Loss {0}%   ok {1} / fail {2}",
        ["stats.outages"] = "Outages {0}   total {1}   longest {2}",
        ["stats.traffic"] = "Traffic down {0} MB  up {1} MB",
        ["stats.speed"] = "Speedtests {0}  best ↓ {1}  ↑ {2} Mbps",
        ["st.ok"] = "ok",
        ["st.tcp"] = "tcp",
        ["st.tcp-timeout"] = "tcp timeout",
        ["st.tcp-refused"] = "tcp refused",
        ["sched.off"] = "Off",
        ["cat.All"] = "All",
        ["cat.App"] = "App",
        ["cat.Outage"] = "Outage",
        ["cat.Ping"] = "Ping",
        ["cat.Threshold"] = "Threshold",
        ["cat.Adapter"] = "Adapter",
        ["cat.Speedtest"] = "Speedtest",
        ["cat.Profile"] = "Profile",
        ["cat.Settings"] = "Settings",
    };
}
