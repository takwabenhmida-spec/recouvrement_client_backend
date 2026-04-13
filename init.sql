-- SCRIPT D'INITIALISATION DE LA BASE DE DONNÉES (PFE - STB BANK)
-- Ce script crée la structure de la base de données 'recouvrement' et insère les données de base.

-- Création de la base si elle n'existe pas
CREATE DATABASE IF NOT EXISTS recouvrement;
USE recouvrement;

-- ---------------------------------------------------------
-- TABLE : agence
-- ---------------------------------------------------------
CREATE TABLE IF NOT EXISTS agence (
    id_agence INT AUTO_INCREMENT PRIMARY KEY,
    ville VARCHAR(100) NOT NULL
) ENGINE=InnoDB;

-- ---------------------------------------------------------
-- TABLE : client
-- ---------------------------------------------------------
CREATE TABLE IF NOT EXISTS client (
    id_client INT AUTO_INCREMENT PRIMARY KEY,
    id_agence INT NOT NULL,
    nom VARCHAR(100) NOT NULL,
    prenom VARCHAR(100) NOT NULL,
    telephone VARCHAR(20),
    email VARCHAR(150),
    token_acces VARCHAR(255),
    cin VARCHAR(20),
    adresse TEXT,
    statut VARCHAR(20) DEFAULT 'Actif',
    token_expire_le DATETIME NULL,
    CONSTRAINT fk_client_agence FOREIGN KEY (id_agence) REFERENCES agence(id_agence) ON DELETE CASCADE
) ENGINE=InnoDB;

-- ---------------------------------------------------------
-- TABLE : utilisateur_back (Agents et Admins)
-- ---------------------------------------------------------
CREATE TABLE IF NOT EXISTS utilisateur_back (
    id_utilisateur_back INT AUTO_INCREMENT PRIMARY KEY,
    id_agence INT NULL,
    nom VARCHAR(100) NOT NULL,
    prenom VARCHAR(100) NOT NULL,
    email VARCHAR(150) NOT NULL UNIQUE,
    mot_de_passe VARCHAR(255) NOT NULL,
    role VARCHAR(50) NOT NULL,
    telephone VARCHAR(20),
    statut VARCHAR(20) DEFAULT 'Actif',
    derniere_connexion DATETIME NULL,
    CONSTRAINT fk_agent_agence FOREIGN KEY (id_agence) REFERENCES agence(id_agence) ON DELETE SET NULL
) ENGINE=InnoDB;

-- ---------------------------------------------------------
-- TABLE : dossier_recouvrement
-- ---------------------------------------------------------
CREATE TABLE IF NOT EXISTS dossier_recouvrement (
    id_dossier INT AUTO_INCREMENT PRIMARY KEY,
    id_client INT NOT NULL,
    montant_initial DECIMAL(18,2) NOT NULL,
    montant_impaye DECIMAL(18,2) NOT NULL,
    frais_dossier DECIMAL(18,2) DEFAULT 0.00,
    statut_dossier VARCHAR(50) NOT NULL, -- aimable, contentieux, regularise
    date_creation DATETIME NOT NULL,
    type_emprunt VARCHAR(100),
    taux_interet DECIMAL(5,2),
    CONSTRAINT fk_dossier_client FOREIGN KEY (id_client) REFERENCES client(id_client) ON DELETE CASCADE
) ENGINE=InnoDB;

-- ---------------------------------------------------------
-- TABLE : echeance
-- ---------------------------------------------------------
CREATE TABLE IF NOT EXISTS echeance (
    id_echeance INT AUTO_INCREMENT PRIMARY KEY,
    id_dossier INT NOT NULL,
    montant DECIMAL(18,2) NOT NULL,
    date_echeance DATETIME NOT NULL,
    statut VARCHAR(20) NOT NULL, -- impaye, paye, partiel
    CONSTRAINT fk_echeance_dossier FOREIGN KEY (id_dossier) REFERENCES dossier_recouvrement(id_dossier) ON DELETE CASCADE
) ENGINE=InnoDB;

-- ---------------------------------------------------------
-- TABLE : historique_paiement
-- ---------------------------------------------------------
CREATE TABLE IF NOT EXISTS historique_paiement (
    id_paiement INT AUTO_INCREMENT PRIMARY KEY,
    id_dossier INT NOT NULL,
    montant_paye DECIMAL(18,2) NOT NULL,
    type_paiement VARCHAR(50), -- espece, virement, chèque
    date_paiement DATETIME NOT NULL,
    CONSTRAINT fk_paiement_dossier FOREIGN KEY (id_dossier) REFERENCES dossier_recouvrement(id_dossier) ON DELETE CASCADE
) ENGINE=InnoDB;

-- ---------------------------------------------------------
-- TABLE : relance_client
-- ---------------------------------------------------------
CREATE TABLE IF NOT EXISTS relance_client (
    id_relance INT AUTO_INCREMENT PRIMARY KEY,
    id_dossier INT NOT NULL,
    moyen VARCHAR(20) NOT NULL, -- email, sms, appel
    statut VARCHAR(20) NOT NULL, -- envoye, repondu
    date_relance DATETIME NOT NULL,
    contenu TEXT,
    CONSTRAINT fk_relance_dossier FOREIGN KEY (id_dossier) REFERENCES dossier_recouvrement(id_dossier) ON DELETE CASCADE
) ENGINE=InnoDB;

-- ---------------------------------------------------------
-- TABLE : communication (Messages Client <-> Banque)
-- ---------------------------------------------------------
CREATE TABLE IF NOT EXISTS communication (
    id_communication INT AUTO_INCREMENT PRIMARY KEY,
    id_dossier INT NOT NULL,
    id_relance INT NULL,
    message TEXT NOT NULL,
    origine VARCHAR(20) NOT NULL, -- client, agent, systeme
    date_envoi DATETIME NOT NULL,
    CONSTRAINT fk_comm_dossier FOREIGN KEY (id_dossier) REFERENCES dossier_recouvrement(id_dossier) ON DELETE CASCADE,
    CONSTRAINT fk_comm_relance FOREIGN KEY (id_relance) REFERENCES relance_client(id_relance) ON DELETE SET NULL
) ENGINE=InnoDB;

-- ---------------------------------------------------------
-- TABLE : intention_client (Réponses aux relances)
-- ---------------------------------------------------------
CREATE TABLE IF NOT EXISTS intention_client (
    id_intention INT AUTO_INCREMENT PRIMARY KEY,
    id_dossier INT NOT NULL,
    type_intention VARCHAR(50) NOT NULL, -- paiement_immediat, promesse_paiement, etc.
    date_intention DATETIME NOT NULL,
    date_paiement_prevue DATETIME NULL,
    montant_propose DECIMAL(18,2) NULL,
    statut VARCHAR(20) DEFAULT 'En attente',
    CONSTRAINT fk_intention_dossier FOREIGN KEY (id_dossier) REFERENCES dossier_recouvrement(id_dossier) ON DELETE CASCADE
) ENGINE=InnoDB;

-- ---------------------------------------------------------
-- TABLE : garantie
-- ---------------------------------------------------------
CREATE TABLE IF NOT EXISTS garantie (
    id_garantie INT AUTO_INCREMENT PRIMARY KEY,
    id_dossier INT NOT NULL,
    type_garantie VARCHAR(50), -- hypotheque, salaire, caution
    description TEXT,
    CONSTRAINT fk_garantie_dossier FOREIGN KEY (id_dossier) REFERENCES dossier_recouvrement(id_dossier) ON DELETE CASCADE
) ENGINE=InnoDB;

-- ---------------------------------------------------------
-- TABLE : score_risque
-- ---------------------------------------------------------
CREATE TABLE IF NOT EXISTS score_risque (
    id_score INT AUTO_INCREMENT PRIMARY KEY,
    id_dossier INT NOT NULL,
    valeur DECIMAL(5,2) NOT NULL,
    points_retard INT DEFAULT 0,
    points_historique INT DEFAULT 0,
    points_garantie INT DEFAULT 0,
    points_intention INT DEFAULT 0,
    niveau VARCHAR(20), -- Faible, Moyen, Élevé
    date_calcul DATETIME NOT NULL,
    CONSTRAINT fk_score_dossier FOREIGN KEY (id_dossier) REFERENCES dossier_recouvrement(id_dossier) ON DELETE CASCADE
) ENGINE=InnoDB;

-- ---------------------------------------------------------
-- TABLE : historique_action
-- ---------------------------------------------------------
CREATE TABLE IF NOT EXISTS historique_action (
    id_action INT AUTO_INCREMENT PRIMARY KEY,
    id_dossier INT NOT NULL,
    action_detail TEXT NOT NULL,
    acteur VARCHAR(50) NOT NULL,
    date_action DATETIME NOT NULL,
    CONSTRAINT fk_action_dossier FOREIGN KEY (id_dossier) REFERENCES dossier_recouvrement(id_dossier) ON DELETE CASCADE
) ENGINE=InnoDB;

-- ---------------------------------------------------------
-- INSERTION DES DONNÉES DE BASE
-- ---------------------------------------------------------

-- 1. Agence par défaut
INSERT INTO agence (id_agence, ville) VALUES (1, 'Tunis - Siege STB');
INSERT INTO agence (id_agence, ville) VALUES (2, 'Sousse');
INSERT INTO agence (id_agence, ville) VALUES (3, 'Sfax');

-- 2. Administrateur par défaut (Password: admin123)
-- Hash BCrypt pour "admin123"
INSERT INTO utilisateur_back (nom, prenom, email, mot_de_passe, role, statut) 
VALUES ('Equipe', 'Admin', 'admin@stb.tn', '$2a$11$R9h/lIPzHZP5h9H9H9H9H.6H9H9H9H9H9H9H9H9H9H9H9H9H9H9H', 'Admin', 'Actif');

-- 3. Un client de test pour la démo
INSERT INTO client (id_agence, nom, prenom, email, telephone, token_acces, cin, adresse, statut)
VALUES (1, 'Ben Salah', 'Ahmed', 'ahmed.bensalah@gmail.com', '21698765432', 'pfe-stb-token-demo-2026', '08765432', 'Avenue Habib Bourguiba, Tunis', 'Actif');

-- 4. Un dossier de test pour le client
INSERT INTO dossier_recouvrement (id_client, montant_initial, montant_impaye, frais_dossier, statut_dossier, date_creation, type_emprunt, taux_interet)
VALUES (1, 5000.00, 1250.00, 50.00, 'aimable', NOW(), 'Crédit Consommation', 7.5);

-- 5. Une échéance impayée
INSERT INTO echeance (id_dossier, montant, date_echeance, statut)
VALUES (1, 1250.00, DATE_SUB(NOW(), INTERVAL 15 DAY), 'impaye');
