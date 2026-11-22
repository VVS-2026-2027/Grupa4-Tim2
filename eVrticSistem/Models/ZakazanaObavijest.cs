using System.ComponentModel.DataAnnotations;

namespace EVrtic.Models
{
    public class ZakazanaObavijest
    {
        public int Id { get; set; }

        public int OdgajateljId { get; set; }

        public int RoditeljId { get; set; }

        [Required]
        [StringLength(150)]
        public string Naslov { get; set; } = string.Empty;

        [Required]
        [StringLength(2000)]
        public string Sadrzaj { get; set; } = string.Empty;

        public DateTime VrijemeSlanjaUtc { get; set; }

        public StatusZakazaneObavijesti Status { get; set; } = StatusZakazaneObavijesti.CEKA;

        public DateTime? DatumSlanjaUtc { get; set; }

        public int BrojPokusaja { get; set; }

        [StringLength(2000)]
        public string? Greska { get; set; }
    }
}
