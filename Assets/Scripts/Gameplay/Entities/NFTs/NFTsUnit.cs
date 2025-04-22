//NFT unit class
public class NFTsUnit : NFTsCard
{
    public int HitPoints { get; set; }

    public int Shield { get; set; }

    public int Damage { get; set; }

    // Ranges to be used by Shooter component
    public float AttackRange { get; set; } = 10f;
    public float DetectionRange { get; set; } = 15f;

    public float Speed { get; set; }
}
