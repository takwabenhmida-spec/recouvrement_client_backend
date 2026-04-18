using System;
using System.Collections.Generic;
using System.Linq;
using RecouvrementAPI.Models;
using RecouvrementAPI.Data;
using Microsoft.EntityFrameworkCore;
namespace RecouvrementAPI.Helpers
{
    public static class RecouvrementHelper
    {
        /// <summary>
        /// Calcule le nombre de jours de retard cumulés sur les échéances impayées.
        /// </summary>
        public static int CalculerJoursRetard(IEnumerable<Echeance> echeances)
        {
            if (echeances == null) return 0;
            var limit = DateTime.UtcNow;
            var impayees = echeances
                .Where(e => e.Statut == AppConstants.EcheanceStatut.Impaye && e.DateEcheance < limit)
                .ToList();
            if (impayees.Count == 0) return 0;
            return (int)(limit - impayees.Min(e => e.DateEcheance)).TotalDays;
        }
        /// <summary>
        /// Calcule les intérêts de retard selon la formule PFE :
        /// Principal * (Taux / 100) * (JoursRetard / 365)
        /// </summary>
        public static decimal CalculerInteretsRetard(decimal principal, decimal taux, int joursRetard)
        {
            // La règle métier STB : on ne calcule d'intérêts qu'après 90 jours
            if (joursRetard < 90) return 0;
            return Math.Round(principal * (taux / 100m) * (joursRetard / 365m), 3);
        }
        /// <summary>
        /// Vérifie si un dossier a plus de 90 jours de retard et déclenche une communication auto si nécessaire.
        /// </summary>
        public static async Task VerifierRetard3Mois(DossierRecouvrement dossier, ApplicationDbContext context)
        {
            int joursRetard = CalculerJoursRetard(dossier.Echeances);
            if (joursRetard <= 90) return;
            // Anti-doublon : pas de communication si une a déjà été envoyée ce mois
            bool dejaEnvoyee = await context.Communications
                .AnyAsync(c =>
                    c.IdDossier == dossier.IdDossier &&
                    c.Origine == AppConstants.SystemActor &&
                    c.DateEnvoi >= DateTime.UtcNow.AddMonths(-1));
            if (!dejaEnvoyee)
            {
                context.Communications.Add(new Communication
                {
                    IdDossier = dossier.IdDossier,
                    Message = $"Alerte automatique : retard de {joursRetard} jours détecté sur votre dossier. Principal : {dossier.MontantImpaye:F3} TND.",
                    Origine = AppConstants.SystemActor,
                    DateEnvoi = DateTime.UtcNow
                });
                context.HistoriqueActions.Add(new HistoriqueAction
                {
                    IdDossier = dossier.IdDossier,
                    ActionDetail = $"Communication auto déclenchée — retard > 3 mois ({joursRetard} jours)",
                    Acteur = AppConstants.SystemActor,
                    DateAction = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
            }
        }
    }
}
