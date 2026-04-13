using Microsoft.ML;
using System.IO;

namespace RecouvrementAPI.ML
{
    public interface IScoringMlService
    {
        string PredictNiveau(ScoringTrainingData input);
        bool IsModelAvailable();
    }

    public class ScoringMlService : IScoringMlService
    {
        private readonly string _modelPath;
        private ITransformer _model;
        private readonly MLContext _mlContext;
        private readonly ILogger<ScoringMlService> _logger;

        public ScoringMlService(ILogger<ScoringMlService> logger)
        {
            _logger = logger;
            _mlContext = new MLContext(seed: 0);
            _modelPath = Path.Combine(Directory.GetCurrentDirectory(), "scoring_model.zip");
            LoadModel();
        }

        private void LoadModel()
        {
            if (File.Exists(_modelPath))
            {
                try
                {
                    DataViewSchema modelSchema;
                    _model = _mlContext.Model.Load(_modelPath, out modelSchema);
                    _logger.LogInformation("Modèle ML.NET chargé avec succès depuis {Path}", _modelPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur lors du chargement du modèle ML.NET.");
                }
            }
            else
            {
                _logger.LogWarning("Le modèle ML.NET n'est pas encore généré ({Path}).", _modelPath);
            }
        }

        public bool IsModelAvailable()
        {
            if (_model == null && File.Exists(_modelPath))
            {
                LoadModel();
            }
            return _model != null;
        }

        public string PredictNiveau(ScoringTrainingData input)
        {
            if (!IsModelAvailable()) return null;

            var predictionEngine = _mlContext.Model.CreatePredictionEngine<ScoringTrainingData, ScoringPrediction>(_model);
            var prediction = predictionEngine.Predict(input);

            _logger.LogInformation("Prédiction ML effectuée : {Label}", prediction.PredictedLabel);
            return prediction.PredictedLabel;
        }
    }
}
