namespace RecouvrementAPI.DTOs
{
    public class RelanceDto
    {
        public int IdRelance { get; set; }   
        public DateTime DateRelance { get; set; }
        public string Moyen { get; set; }
        public string Statut { get; set; }
        public string Contenu { get; set; }
    }
}