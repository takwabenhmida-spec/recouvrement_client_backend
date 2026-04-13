# Diagramme de Classes - Espace Client
*(Mis à jour pour correspondre parfaitement aux données des User Stories US01 à US13 et au code backend)*

Ce diagramme lie précisément chaque entité, DTO et méthode de contrôle aux exigences définies dans le tableau des User Stories.

```mermaid
classDiagram
    direction LR

    %% ==========================================
    %% US01 & US02: AUTHENTIFICATION CLIENT
    %% ==========================================
    class Client {
        <<Entité>>
        +int IdClient
        +string TokenAcces
        +DateTime? TokenExpireLe
        +string Nom
        +string Prenom
    }

    %% ==========================================
    %% CLASSES DIALOGUE (Vues du Frontend - US13)
    %% ==========================================
    class ClientHistoriqueDto {
        <<Dialogue>>
        +string NomComplet
    }
    class DossierDto {
        <<Dialogue, US03, US04>>
        +int IdDossier
        +string TypeEmprunt
        +decimal MontantInitial
        +decimal MontantPaye
        +decimal MontantImpaye
        +decimal FraisDossier
        +int NombreJoursRetard
        +string StatutDossier
    }
    class SubmitIntentionDto {
        <<Dialogue, US08>>
        +string TypeIntention
        +decimal? MontantPropose
        +DateTime? DatePaiementPrevue
        +string Commentaire
    }
    class EnvoyerMessageDto {
        <<Dialogue, US07, US10>>
        +string Contenu
    }

    %% ==========================================
    %% CONTRÔLEUR CENTRAL (Point d'accès logique)
    %% ==========================================
    class ClientController {
        <<Contrôle>>
        -VerifierToken(token)
        +GetHistorique(token) 
        +RepondreRelance(token, idRelance, dto) 
        +PostIntention(token, dto) 
        +EnvoyerMessage(token, dto)
        +GenerateRecu(token, idDossier) 
        +GenerateHistoriquePdf(token, idDossier) 
    }

    %% ==========================================
    %% ENTITÉS DU DOMAINE (La base de données)
    %% ==========================================
    class DossierRecouvrement {
        <<Entité, US03>>
        +int IdDossier
        +decimal MontantInitial
        +decimal MontantImpaye
        +string TypeEmprunt
    }
    class Echeance {
        <<Entité, US04>>
        +int IdEcheance
        +decimal Montant
        +DateTime DateEcheance
        +string Statut
    }
    class HistoriquePaiement {
        <<Entité, US05>>
        +int IdPaiement
        +decimal MontantPaye
        +DateTime DatePaiement
        +string TypePaiement
    }
    class RelanceClient {
        <<Entité, US06>>
        +int IdRelance
        +DateTime DateRelance
        +string Contenu
        +string Statut
    }
    class Communication {
        <<Entité, US10>>
        +int IdCommunication
        +string Message
        +string Origine
        +DateTime DateEnvoi
    }
    class IntentionClient {
        <<Entité, US08>>
        +int IdIntention
        +string TypeIntention
        +DateTime DateIntention
        +decimal? MontantPropose
        +DateTime? DatePaiementPrevue
        +string Statut
    }
    class HistoriqueAction {
        <<Entité, US12>>
        +int IdAction
        +string ActionDetail
        +DateTime DateAction
    }

    %% ==========================================
    %% RELATIONS EXPLICITES
    %% ==========================================
    
    %% Liens Action/Client -> Contrôle -> Vue
    ClientController --> Client : US01, US02 (Validation Token)
    ClientController ..> ClientHistoriqueDto : Instancie
    ClientController ..> DossierDto : Construit
    ClientController ..> EnvoyerMessageDto : Reçoit (US07, US10)
    ClientController ..> SubmitIntentionDto : Reçoit (US08)
    
    %% Hiérarchie des classes de base de données
    Client "1" --> "*" DossierRecouvrement : possède
    ClientHistoriqueDto "1" --> "*" DossierDto : inclut
    
    %% Contenu d'un dossier
    DossierRecouvrement "1" --> "*" Echeance : planifie (US04)
    DossierRecouvrement "1" --> "*" HistoriquePaiement : trace (US05)
    DossierRecouvrement "1" --> "*" RelanceClient : regroupe (US06)
    DossierRecouvrement "1" --> "*" Communication : liste (US10)
    DossierRecouvrement "1" --> "*" IntentionClient : reçoit (US08)
    DossierRecouvrement "1" --> "*" HistoriqueAction : archive (US12)

    %% Liens spécifiques
    RelanceClient "1" --> "*" Communication : lie réponse client (US07)
```
