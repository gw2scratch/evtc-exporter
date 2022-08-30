using System.Text.Json;
using System.Text.Json.Serialization;
using GW2Scratch.EVTCAnalytics;
using GW2Scratch.EVTCAnalytics.Events;
using GW2Scratch.EVTCAnalytics.Model.Agents;
using GW2Scratch.EVTCAnalytics.Processing;

if (args.Length < 1)
{
    Console.WriteLine("Usage: ./EvtcExport [evtc_filename]");
    return 1;
}

var filename = args[0];
var log = new LogProcessor().ProcessLog(filename, new EVTCParser());

var minTime = log.Events.Select(x => x.Time).Min();
var hitEvents = log.Events.OfType<PhysicalDamageEvent>().GroupBy(x => x.Attacker);

var exposedResults = new[]
    { PhysicalDamageEvent.Result.Normal, PhysicalDamageEvent.Result.Critical, PhysicalDamageEvent.Result.Glance };

var attackers = hitEvents.Select(x =>
{
    var hits = x.Where(e => exposedResults.Contains(e.HitResult)).Select(e => new PhysicalHit(
        e.Time - minTime,
        e.Skill.Id,
        e.Damage,
        e.HitResult switch
        {
            PhysicalDamageEvent.Result.Normal => HitResult.Normal,
            PhysicalDamageEvent.Result.Critical => HitResult.Critical,
            PhysicalDamageEvent.Result.Glance => HitResult.Glance,
            _ => throw new ArgumentOutOfRangeException()
        })).ToList();

    var attacker = x.Key switch
    {
        Player player => new Attacker(player.Name, AttackerType.Player, player.AccountName[1..], hits),
        NPC npc => new Attacker(npc.Name, AttackerType.NPC, null, hits),
        Gadget gadget => new Attacker(gadget.Name, AttackerType.Gadget, null, hits),
        _ => throw new ArgumentOutOfRangeException()
    };

    return attacker;
}).ToList();

Console.WriteLine(JsonSerializer.Serialize(attackers));

return 0;

enum AttackerType
{
    Player,
    NPC,
    Gadget
}

enum HitResult
{
    Normal = 0,
    Critical = 1,
    Glance = 2,
    // Ignores and interrupts are not exposed.
}

record PhysicalHit(long Time, uint SkillId, int Damage, [property: JsonConverter(typeof(JsonStringEnumConverter))] HitResult Result);

record Attacker(string Name, [property: JsonConverter(typeof(JsonStringEnumConverter))] AttackerType Type, string? Account, List<PhysicalHit> Hits);