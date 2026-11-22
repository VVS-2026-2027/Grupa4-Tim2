namespace EVrtic.Models;

public class NajavaOdsustva
{
    public int Id { get; set; }
    public int RoditeljId { get; set; }
    public int DijeteId { get; set; }
    public DateTime DatumOd { get; set; }
    public DateTime DatumDo { get; set; }
    public string Razlog { get; set; } = string.Empty;
    public StatusNajaveOdsustva Status { get; set; }
    public DateTime DatumKreiranja { get; set; }
}
