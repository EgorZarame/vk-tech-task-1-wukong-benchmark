using System.Text;

namespace WukongBenchRunner.Config;

public interface IGameConfig
{
    /// <summary>Если прошлый запуск был убит и оставил бэкап — вернуть оригинал.</summary>
    bool RecoverAfterCrash();

    void Backup();

    void Apply(SettingsPatch patch);

    void Restore();
}

/// <summary>
/// GameUserSettings.ini с резервной копией. Пока инструмент работает, рядом лежит
/// <c>.wbr-backup</c>; его наличие при старте означает, что прошлый запуск не успел восстановить конфиг.
/// </summary>
public sealed class GameConfigFile(string path) : IGameConfig
{
    public string Path { get; } = path;
    public string BackupPath { get; } = path + ".wbr-backup";

    public bool RecoverAfterCrash()
    {
        if (!File.Exists(BackupPath)) return false;
        Restore();
        return true;
    }

    public void Backup()
    {
        if (!File.Exists(Path))
        {
            throw new FileNotFoundException(
                $"Не найден конфиг бенчмарка: {Path}. Запустите бенчмарк вручную хотя бы один раз.", Path);
        }
        File.Copy(Path, BackupPath, overwrite: true);
    }

    public void Apply(SettingsPatch patch)
    {
        string text;
        Encoding encoding;
        // Unreal может сохранять ini в UTF-16 — читаем с определением BOM и пишем в той же кодировке.
        using (var reader = new StreamReader(Path, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true))
        {
            text = reader.ReadToEnd();
            encoding = reader.CurrentEncoding;
        }

        File.WriteAllText(Path, patch.ApplyTo(text), encoding);
    }

    public void Restore()
    {
        if (!File.Exists(BackupPath)) return;
        File.Copy(BackupPath, Path, overwrite: true);
        File.Delete(BackupPath);
    }
}
