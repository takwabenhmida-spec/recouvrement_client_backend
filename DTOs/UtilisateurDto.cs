using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RecouvrementAPI.DTOs
{
    public class UtilisateurItemDto
    {
        public int IdAgent { get; set; }
        public string NomComplet { get; set; }
        public string Email { get; set; }
        public string Telephone { get; set; }
        public string Role { get; set; }
        public string Agence { get; set; }
        public int? IdAgence { get; set; }
        public string DerniereConnexion { get; set; }
        public string Statut { get; set; }
    }

    public class CreateUtilisateurDto
    {
        [Required] public string Nom { get; set; }
        [Required] public string Prenom { get; set; }
        [Required] [EmailAddress] public string Email { get; set; }
        [Required] public string Telephone { get; set; }
        [Required] public string MotDePasse { get; set; }
        [Required] public string Role { get; set; }
        public int? IdAgence { get; set; }
    }
    
    public class UpdateUtilisateurDto
    {
        public string Nom { get; set; }
        public string Prenom { get; set; }
        public string Telephone { get; set; }
        public string Role { get; set; }
        public int? IdAgence { get; set; }
    }

    public class UtilisateurListResponseDto
    {
        public List<UtilisateurItemDto> Items { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
    }
}
