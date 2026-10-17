using UnityEngine;
namespace SinkScrapt
{
    [CreateAssetMenu(menuName="SinkScrapt/Balance")]
    public class BalanceConfig : ScriptableObject
    {
        [Tooltip("Expedition length in seconds. The sinking milestones in ShipTimeline scale with it (nominal 600).")]
        public float duration=600;
        [Tooltip("Expedition length on the abandoned cruise ship; its milestones (ShipProfile.Cruise) scale with it (nominal 720: steady for five minutes, then seven of sinking).")]
        public float cruiseDuration=720;
        public float breath=35;
        public float drowningDamage=10;
        public float walkSpeed=4.5f;
        public float sprintSpeed=7;
        public float swimSpeed=3;
        [Tooltip("Speed multiplier while wading through water below chest height (the bilge on the lowest deck).")]
        public float wadeSpeedFactor=.68f;
        public float climbSpeed=2.5f;
        public float flashlightRange=28;
        [Tooltip("Intensity of a lit torch's 40-degree hotspot (URP, inverse-square falloff); the wide spill around it gets 30% of this.")]
        public float flashlightIntensity=60;
        [Tooltip("Full cone angle of the torch's dim spill in degrees, around the narrow hotspot.")]
        public float flashlightAngle=100;
        [Tooltip("Seconds of light from one full flashlight battery.")]
        public float flashlightBattery=150;
        [Tooltip("How far (metres from the eyes) a player can reach to pick something up or use it.")]
        public float reach=4;
        public float flareDuration=20;
        public float voiceDistance=15;
        [Header("Hull stalker (dry-space monster)")]
        public float stalkerPatrolSpeed=2.1f;
        public float stalkerChaseSpeed=4.6f;
        public float stalkerSight=16;
        public float stalkerDamage=30;
        [Header("Drowned hunter (water monster)")]
        public float drownedPatrolSpeed=2.4f;
        public float drownedChaseSpeed=3.7f;
        public float drownedSight=14;
        public float drownedDamage=35;
        [Header("Shared monster rules")]
        public float attackReach=1.9f;
        public float attackWindUp=.65f;
        public float attackCooldown=2.4f;
        public float searchSeconds=7;
        [Tooltip("On first sight of a diver a monster stops, faces them and screams this long before it gives chase.")]
        public float screamSeconds=1.3f;
        [Tooltip("A monster screams at most once in this many seconds; otherwise sighting a diver goes straight to the chase.")]
        public float screamCooldown=15;
        [Tooltip("Chance that a hull stalker in reach grabs and throws its victim instead of striking.")]
        [Range(0,1)] public float grabChance=.35f;
        [Tooltip("Damage when the grabbed diver is thrown.")]
        public float grabDamage=22;
        [Tooltip("Speed (m/s) at which a grabbed diver is flung away.")]
        public float throwSpeed=8;
        [Tooltip("Shove (m/s) from an ordinary strike.")]
        public float hitKnockback=3.5f;
        [Header("Boat computer orders")]
        [Tooltip("Seconds between paying on the boat computer and the parachute package landing on the small boat.")]
        public float deliverySeconds=30;
        [Header("Presentation")]
        [Tooltip("Length of the arrival cutscene at the wreck or the island. The expedition clock starts when it ends.")]
        public float arrivalCutscene=7;
        [Header("Boat computer")]
        [Tooltip("How long a steam leak or live-electricity spot stays off after the boat computer shuts it down.")]
        public float hazardOffSeconds=30;
        [Tooltip("Wait after a shut-down ends before the same hazard can be shut down again.")]
        public float hazardCooldown=60;
    }
}
