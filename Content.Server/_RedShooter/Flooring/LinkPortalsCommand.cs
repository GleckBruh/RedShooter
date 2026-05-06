using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Teleportation.Components;
using Robust.Shared.Console;
using Content.Shared.Teleportation.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server._RedShooter.Flooring;

[AdminCommand(AdminFlags.Mapping)]
public sealed class LinkPortalsCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entMan = default!;

    public override string Command => "linkportals";
    public override string Description => "Связывает два портала. Использование: linkportals <uid1> <uid2>";
    public override string Help => "linkportals 1234 5678";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError("Нужно два аргумента: linkportals <uid1> <uid2>");
            return;
        }

        if (!NetEntity.TryParse(args[0], out var netA) ||
            !NetEntity.TryParse(args[1], out var netB))
        {
            shell.WriteError("Не удалось распарсить EntityUid.");
            return;
        }

        var a = _entMan.GetEntity(netA);
        var b = _entMan.GetEntity(netB);

        if (!_entMan.EntityExists(a) || !_entMan.EntityExists(b))
        {
            shell.WriteError("Одна из сущностей не существует.");
            return;
        }

        if (!_entMan.TryGetComponent<LinkedEntityComponent>(a, out var linkA))
        {
            shell.WriteError($"{args[0]} не имеет LinkedEntityComponent. Это портал?");
            return;
        }

        if (!_entMan.TryGetComponent<LinkedEntityComponent>(b, out var linkB))
        {
            shell.WriteError($"{args[1]} не имеет LinkedEntityComponent. Это портал?");
            return;
        }

        var sys = _entMan.System<LinkedEntitySystem>();
        sys.TryLink(a, b, true);

        var nameA = _entMan.GetComponent<MetaDataComponent>(a).EntityName;
        var nameB = _entMan.GetComponent<MetaDataComponent>(b).EntityName;

        shell.WriteLine($"✓ Порталы связаны:");
        shell.WriteLine($"  [{args[0]}] \"{nameA}\"  ↔  [{args[1]}] \"{nameB}\"");
    }
}
