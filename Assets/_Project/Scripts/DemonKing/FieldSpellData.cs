using UnityEngine;

namespace Game.DemonKing
{
    public enum FieldSpellKind { Tornado, Lightning, Earthquake, Meteor, ManaBurst, Downpour }

    /// 魔王のフィールド魔法（ロビーで5つ装備し、試合中に 1〜5 キーで使う）。使うのはクールタイムだけ（MP は使わない）。
    /// アセットは Resources/FieldSpells/ に置く（ロビーと試合の両方で Resources.LoadAll で読む）。
    [CreateAssetMenu(menuName = "Game/Field Spell Data", fileName = "FieldSpell")]
    public class FieldSpellData : ScriptableObject
    {
        public FieldSpellKind kind;
        public string spellName;
        [TextArea(2, 5)] public string description;
        [Tooltip("Single character drawn on the card.")]
        public string glyph = "?";
        public Color color = new Color(0.4f, 0.2f, 0.5f);
        [Tooltip("Lobby order.")]
        public int order;

        [Header("Use")]
        [Tooltip("Seconds before it can be used again.")]
        public float cooldown = 40f;
        [Tooltip("Seconds from the cast to the main effect (the warning heroes can see).")]
        public float chargeSeconds = 5f;
        [Tooltip("Needs a point on the field (false = the whole map, e.g. Earthquake).")]
        public bool needsTarget = true;
        [Tooltip("The point picks a whole biome (Downpour).")]
        public bool targetsBiome;

        [Header("Area and Damage")]
        [Tooltip("Main radius (m): the tornado, the direct lightning hit, a meteor impact, the mana burst.")]
        public float radius = 6f;
        [Tooltip("Damage inside the main radius.")]
        public int damage = 40;
        [Tooltip("Outer radius (m): lightning's side shock.")]
        public float outerRadius;
        public int outerDamage;
        [Tooltip("Seconds the effect lasts after it starts (tornado, rain).")]
        public float duration = 3f;
        [Tooltip("How many (meteors).")]
        public int count = 1;
        [Tooltip("Radius over which the meteors are scattered (m).")]
        public float scatterRadius;

        [Header("Extra")]
        [Tooltip("Upward launch speed (m/s) of heroes caught by the tornado.")]
        public float launchSpeed = 16f;
        [Tooltip("Rocks the tornado throws.")]
        public int debrisCount;
        public int debrisDamage;
        [Tooltip("Slow left by the lightning side shock / the rain: seconds and speed multiplier.")]
        public float slowSeconds;
        public float slowMultiplier = 1f;
        [Tooltip("Lightning damage multiplier while the ground is wet from Downpour.")]
        public float wetDamageMultiplier = 1.5f;
        [Tooltip("Sideways wind push in the rain (m/s), switching left and right.")]
        public float windPush;
        [Tooltip("Seconds the ground stays wet after the rain stops.")]
        public float wetAfterSeconds = 10f;
        [Tooltip("Mana burst: seconds before it bursts (random between).")]
        public Vector2 fuseSeconds = new Vector2(3f, 5f);
        [Tooltip("Mana burst: HP of the orb. Any hit bursts it early.")]
        public int orbHp = 1;
        [Tooltip("Crater left by a meteor (radius m, depth m).")]
        public Vector2 crater = new Vector2(3f, 1f);
    }
}
