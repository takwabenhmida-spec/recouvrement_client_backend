using Microsoft.ML;
using Microsoft.EntityFrameworkCore;
using RecouvrementAPI.Data;
using RecouvrementAPI.Models;
using System.IO;

namespace RecouvrementAPI.ML
{
    public interface IModelTrainer
    {
        Task<int> TrainModelAsync();
    }

    public class ModelTrainer : IModelTrainer
    {
        private readonly ApplicationDbContext _context;
        private readonly string _modelPath;
        private readonly ILogger<ModelTrainer> _logger;

        public ModelTrainer(ApplicationDbContext context, ILogger<ModelTrainer> logger)
        {
            _context = context;
            _logger = logger;
            _modelPath = Path.Combine(Directory.GetCurrentDirectory(), "scoring_model.zip");
        }

        public async Task<int> TrainModelAsync()
        {
            _logger.LogInformation("Démarrage de l'entraînement du modèle ML.NET...");

            // 1. Charger les données labellisées (Dossiers avec leurs scores calculés)
            var dossiers = await _context.Dossiers
                .Include(d => d.ScoresRisque)
                .Include(d => d.Echeances)
                .Include(d => d.Garanties)
                .Include(d => d.Intentions)
                .Where(d => d.ScoresRisque.Any())
                .ToListAsync();

            if (dossiers.Count < 5) // Minimum requis pour que ça ait du sens
            {
                _logger.LogWarning("Pas assez de données pour l'entraînement (min 5).");
                return 0;
            }

            var trainingData = dossiers.Select(d => {
                var dernierScore = d.ScoresRisque.OrderByDescending(s => s.DateCalcul).First();
                var impayees = d.Echeances.Where(e => e.Statut == "impaye" && e.DateEcheance < DateTime.Now).ToList();
                int retardJours = impayees.Any() ? (int)(DateTime.Now - impayees.Min(e => e.DateEcheance)).TotalDays : 0;
                
                var derniereIntention = d.Intentions.OrderByDescending(i => i.DateIntention).FirstOrDefault();

                return new ScoringTrainingData
                {
                    RetardJours = retardJours,
                    NbEcheancesImpayees = impayees.Count,
                    HasGarantie = d.Garanties.Any() ? 1f : 0f,
                    GarantieForte = d.Garanties.Any(g => g.TypeGarantie == "hypotheque" || g.TypeGarantie == "salaire") ? 1f : 0f,
                    MontantImpaye = (float)d.MontantImpaye,
                    HasIntention = d.Intentions.Any() ? 1f : 0f,
                    IntentionPositive = (derniereIntention?.TypeIntention == "paiement_immediat" || derniereIntention?.TypeIntention == "promesse_paiement") ? 1f : 0f,
                    Niveau = dernierScore.Niveau
                };
            }).ToList();

            // 2. Initialiser ML.NET
            var mlContext = new MLContext(seed: 0);

            // 3. Charger dans IDataView
            var dataView = mlContext.Data.LoadFromEnumerable(trainingData);

            // 4. Définir le pipeline
            var pipeline = mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(ScoringTrainingData.Niveau))
                .Append(mlContext.Transforms.Concatenate("Features", 
                    nameof(ScoringTrainingData.RetardJours), 
                    nameof(ScoringTrainingData.NbEcheancesImpayees), 
                    nameof(ScoringTrainingData.HasGarantie), 
                    nameof(ScoringTrainingData.GarantieForte), 
                    nameof(ScoringTrainingData.MontantImpaye), 
                    nameof(ScoringTrainingData.HasIntention), 
                    nameof(ScoringTrainingData.IntentionPositive)))
                .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
                .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            // 5. Entraîner le modèle
            var model = pipeline.Fit(dataView);

            // 6. Sauvegarder
            mlContext.Model.Save(model, dataView.Schema, _modelPath);

            _logger.LogInformation("Modèle ML.NET entraîné et sauvegardé à {Path}", _modelPath);
            return trainingData.Count;
        }
    }
}
