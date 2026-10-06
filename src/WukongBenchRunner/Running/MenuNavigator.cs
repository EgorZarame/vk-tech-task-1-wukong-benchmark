namespace WukongBenchRunner.Running;

/// <summary>
/// Положение кнопок меню в долях клиентской области окна — так координаты
/// не зависят от выбранного разрешения.
/// </summary>
public sealed record MenuLayout(
    double StartButtonX = 0.10,
    double StartButtonY = 0.45,
    double ConfirmButtonX = 0.39,
    double ConfirmButtonY = 0.59);

/// <summary>
/// Штатного способа запустить тест без меню нет (ни ключей командной строки, ни настройки в конфиге),
/// поэтому меню проходится циклом из трёх действий:
/// Enter — пропустить заставку → клик «Тест быстродействия» → клик «Подтвердить».
/// Цикл повторяется, пока не появится файл результата: если экран загрузился позже
/// и действие ушло «в пустоту», следующий круг его повторит. Во время самого теста
/// эти нажатия ничего не делают.
/// </summary>
public sealed class MenuNavigator(IBenchmarkProcess process, IInputSimulator input, MenuLayout layout) : IMenuNavigator
{
    public bool TryAdvance(int step)
    {
        var window = process.FindMainWindow();
        if (window == 0) return false;

        // Нажимаем только в своё окно — иначе можно кликнуть по чужому приложению.
        if (!input.IsForeground(window) && !input.TryActivate(window)) return false;

        switch (step % 3)
        {
            case 0:
                input.PressKey(Win32KeyCodes.Return);
                break;
            case 1:
                input.ClickRelative(window, layout.StartButtonX, layout.StartButtonY);
                break;
            default:
                input.ClickRelative(window, layout.ConfirmButtonX, layout.ConfirmButtonY);
                break;
        }
        return true;
    }
}

public static class Win32KeyCodes
{
    public const ushort Return = 0x0D;
}
