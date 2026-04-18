using System;
using System.Collections.Generic;
using RecouvrementAPI.Models;
using Xunit;

namespace RecouvrementAPI.Tests
{
    public class ModelCoverageTests
    {
        [Fact]
        public void Agence_Coverage()
        {
            var model = new Agence { IdAgence = 1, Ville = "Tunis", Clients = new List<Client>() };
            // Read properties to satisfy coverage tools (get)
            _ = model.IdAgence;
            _ = model.Ville;
            _ = model.Clients;
            Assert.Equal(1, model.IdAgence);
        }

        [Fact]
        public void Client_Coverage()
        {
            var date = DateTime.UtcNow;
            var agence = new Agence();
            var model = new Client 
            { 
                IdClient = 1, IdAgence = 1, Nom = "N", Prenom = "P", Email = "E", 
                TokenAcces = "T", TokenExpireLe = date, Statut = "S", CIN = "C",
                Telephone = "00", Adresse = "A", Agence = agence,
                Dossiers = new List<DossierRecouvrement>()
            };
            _ = model.IdClient; _ = model.IdAgence; _ = model.Nom; _ = model.Prenom;
            _ = model.Email; _ = model.TokenAcces; _ = model.TokenExpireLe;
            _ = model.Statut; _ = model.CIN; _ = model.Telephone; _ = model.Adresse;
            _ = model.Agence; _ = model.Dossiers;
            Assert.Equal(1, model.IdClient);
        }

        [Fact]
        public void Communication_Coverage()
        {
            var date = DateTime.UtcNow;
            var relance = new RelanceClient();
            var dossier = new DossierRecouvrement();
            var model = new Communication 
            { 
                IdCommunication = 1, IdDossier = 1, Message = "M", Origine = "O", 
                DateEnvoi = date, IdRelance = 1, Relance = relance, Dossier = dossier 
            };
            _ = model.IdCommunication; _ = model.IdDossier; _ = model.Message;
            _ = model.Origine; _ = model.DateEnvoi; _ = model.IdRelance;
            _ = model.Relance; _ = model.Dossier;
            Assert.Equal(1, model.IdCommunication);
        }

        [Fact]
        public void DossierRecouvrement_Coverage()
        {
            var date = DateTime.UtcNow;
            var client = new Client();
            var model = new DossierRecouvrement
            {
                IdDossier = 1, IdClient = 1, TypeEmprunt = "T", MontantInitial = 100, 
                MontantImpaye = 50, FraisDossier = 10, TauxInteret = 5, 
                StatutDossier = "S", DateCreation = date, Client = client,
                Echeances = new List<Echeance>(),
                HistoriquePaiements = new List<HistoriquePaiement>(),
                HistoriqueActions = new List<HistoriqueAction>(),
                Intentions = new List<IntentionClient>(),
                Garanties = new List<Garantie>(),
                Relances = new List<RelanceClient>(),
                Communications = new List<Communication>(),
                ScoresRisque = new List<ScoreRisque>()
            };
            _ = model.IdDossier; _ = model.IdClient; _ = model.TypeEmprunt;
            _ = model.MontantInitial; _ = model.MontantImpaye; _ = model.FraisDossier;
            _ = model.TauxInteret; _ = model.StatutDossier; _ = model.DateCreation;
            _ = model.Client; _ = model.Echeances; _ = model.HistoriquePaiements;
            _ = model.HistoriqueActions; _ = model.Intentions; _ = model.Garanties;
            _ = model.Relances; _ = model.Communications; _ = model.ScoresRisque;
            Assert.Equal(1, model.IdDossier);
        }

        [Fact]
        public void Echeance_Coverage()
        {
            var date = DateTime.UtcNow;
            var dossier = new DossierRecouvrement();
            var model = new Echeance 
            { 
                IdEcheance = 1, IdDossier = 1, Montant = 100, DateEcheance = date, 
                Statut = "S", Dossier = dossier 
            };
            _ = model.IdEcheance; _ = model.IdDossier; _ = model.Montant;
            _ = model.DateEcheance; _ = model.Statut; _ = model.Dossier;
            Assert.Equal(1, model.IdEcheance);
        }

        [Fact]
        public void Garantie_Coverage()
        {
            var dossier = new DossierRecouvrement();
            var model = new Garantie 
            { 
                IdGarantie = 1, IdDossier = 1, TypeGarantie = "T", Description = "D", Dossier = dossier 
            };
            _ = model.IdGarantie; _ = model.IdDossier; _ = model.TypeGarantie;
            _ = model.Description; _ = model.Dossier;
            Assert.Equal(1, model.IdGarantie);
        }

        [Fact]
        public void HistoriqueAction_Coverage()
        {
            var date = DateTime.UtcNow;
            var dossier = new DossierRecouvrement();
            var model = new HistoriqueAction 
            { 
                IdAction = 1, IdDossier = 1, ActionDetail = "A", Acteur = "U", 
                DateAction = date, Dossier = dossier 
            };
            _ = model.IdAction; _ = model.IdDossier; _ = model.ActionDetail;
            _ = model.Acteur; _ = model.DateAction; _ = model.Dossier;
            Assert.Equal(1, model.IdAction);
        }

        [Fact]
        public void HistoriquePaiement_Coverage()
        {
            var date = DateTime.UtcNow;
            var dossier = new DossierRecouvrement();
            var model = new HistoriquePaiement 
            { 
                IdPaiement = 1, IdDossier = 1, MontantPaye = 100, DatePaiement = date, 
                TypePaiement = "T", Dossier = dossier 
            };
            _ = model.IdPaiement; _ = model.IdDossier; _ = model.MontantPaye;
            _ = model.DatePaiement; _ = model.TypePaiement; _ = model.Dossier;
            Assert.Equal(1, model.IdPaiement);
        }

        [Fact]
        public void IntentionClient_Coverage()
        {
            var date = DateTime.UtcNow;
            var dateP = DateTime.UtcNow.AddDays(7);
            var dossier = new DossierRecouvrement();
            var model = new IntentionClient 
            { 
                IdIntention = 1, IdDossier = 1, TypeIntention = "T", DateIntention = date, 
                DatePaiementPrevue = dateP, MontantPropose = 100, Statut = "S", Dossier = dossier 
            };
            _ = model.IdIntention; _ = model.IdDossier; _ = model.TypeIntention;
            _ = model.DateIntention; _ = model.DatePaiementPrevue;
            _ = model.MontantPropose; _ = model.Statut; _ = model.Dossier;
            Assert.Equal(1, model.IdIntention);
        }

        [Fact]
        public void RelanceClient_Coverage()
        {
            var date = DateTime.UtcNow;
            var dossier = new DossierRecouvrement();
            var model = new RelanceClient 
            { 
                IdRelance = 1, IdDossier = 1, DateRelance = date, Moyen = "M", 
                Statut = "S", Contenu = "C", Dossier = dossier 
            };
            _ = model.IdRelance; _ = model.IdDossier; _ = model.DateRelance;
            _ = model.Moyen; _ = model.Statut; _ = model.Contenu; _ = model.Dossier;
            Assert.Equal(1, model.IdRelance);
        }

        [Fact]
        public void ScoreRisque_Coverage()
        {
            var date = DateTime.UtcNow;
            var dossier = new DossierRecouvrement();
            var model = new ScoreRisque 
            { 
                IdScore = 1, IdDossier = 1, ScoreTotal = 100, PointsRetard = 50, 
                PointsHistorique = 20, PointsGarantie = 20, PointsIntention = 10, 
                Niveau = "N", DateCalcul = date, Dossier = dossier 
            };
            _ = model.IdScore; _ = model.IdDossier; _ = model.ScoreTotal;
            _ = model.PointsRetard; _ = model.PointsHistorique; _ = model.PointsGarantie;
            _ = model.PointsIntention; _ = model.Niveau; _ = model.DateCalcul;
            _ = model.Dossier;
            Assert.Equal(1, model.IdScore);
        }

        [Fact]
        public void UtilisateurBack_Coverage()
        {
            var agence = new Agence();
            var model = new UtilisateurBack 
            { 
                IdAgent = 1, IdAgence = 1, Nom = "N", Prenom = "P", Email = "E", 
                MotDePasse = "M", Role = "R", Statut = "S", Agence = agence 
            };
            _ = model.IdAgent; _ = model.IdAgence; _ = model.Nom; _ = model.Prenom;
            _ = model.Email; _ = model.MotDePasse; _ = model.Role; _ = model.Statut;
            _ = model.Agence;
            Assert.Equal(1, model.IdAgent);
        }
    }
}
