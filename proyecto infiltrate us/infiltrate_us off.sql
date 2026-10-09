-- =====================================================================
-- INFILTRATE US  ·  Base de datos completa hasta el Paso 2
-- Para IMPORTAR en phpMyAdmin (XAMPP): pestaña Importar > elegir este archivo > Continuar
--
-- Contiene: la base infiltrate_us, 7 tablas (Jugadores y Tienda),
--           los 5 niveles, las 4 categorías y 9 artículos de prueba.
--
-- Se puede importar más de una vez sin problema: no borra nada y no duplica datos
-- (CREATE TABLE IF NOT EXISTS + INSERT IGNORE).
-- =====================================================================

CREATE DATABASE IF NOT EXISTS infiltrate_us
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE infiltrate_us;


-- #####################################################################
-- MÓDULO 1 · JUGADORES
-- #####################################################################

-- ---------------------------------------------------------------------
-- 1) level  ·  tabla madre (no depende de ninguna)
--    Catálogo de niveles: cuánta XP se necesita y qué premio da cada uno.
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS level (
  level_id      INT          NOT NULL AUTO_INCREMENT,
  level_number  INT          NOT NULL,
  level_name    VARCHAR(40)  NOT NULL,
  xp_required   INT          NOT NULL,
  token_reward  INT          NOT NULL DEFAULT 0,
  PRIMARY KEY (level_id),
  UNIQUE KEY uk_level_number (level_number)
) ENGINE=InnoDB;

-- ---------------------------------------------------------------------
-- 2) game_character  ·  tabla madre
--    Los personajes que diseña el grupo.
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS game_character (
  character_id    INT           NOT NULL AUTO_INCREMENT,
  character_name  VARCHAR(50)   NOT NULL,
  career          VARCHAR(80)   NOT NULL,
  description     VARCHAR(255)  NULL,
  sprite_path     VARCHAR(255)  NULL,
  is_default      BOOLEAN       NOT NULL DEFAULT FALSE,
  PRIMARY KEY (character_id),
  UNIQUE KEY uk_character_name (character_name)
) ENGINE=InnoDB;

-- ---------------------------------------------------------------------
-- 3) player  ·  depende de level y game_character
--    La cuenta de cada jugador.
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS player (
  player_id              INT           NOT NULL AUTO_INCREMENT,
  username               VARCHAR(30)   NOT NULL,
  email                  VARCHAR(100)  NOT NULL,
  password_hash          VARCHAR(255)  NOT NULL,
  tokens                 INT           NOT NULL DEFAULT 0,
  xp                     INT           NOT NULL DEFAULT 0,
  level_id               INT           NOT NULL DEFAULT 1,
  selected_character_id  INT           NULL,
  is_online              BOOLEAN       NOT NULL DEFAULT FALSE,
  last_login_at          DATETIME      NULL,
  created_at             DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
  deleted_at             DATETIME      NULL,
  PRIMARY KEY (player_id),
  UNIQUE KEY uk_player_username (username),
  UNIQUE KEY uk_player_email (email),
  CONSTRAINT fk_player_level
    FOREIGN KEY (level_id) REFERENCES level (level_id)
    ON DELETE RESTRICT,
  CONSTRAINT fk_player_character
    FOREIGN KEY (selected_character_id) REFERENCES game_character (character_id)
    ON DELETE SET NULL
) ENGINE=InnoDB;

-- ---------------------------------------------------------------------
-- 4) friendship  ·  CONECTORA N:M entre player y player
--    Sin id propio: su PK es la pareja (quien pide, quien recibe).
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS friendship (
  requester_id  INT       NOT NULL,
  addressee_id  INT       NOT NULL,
  status        ENUM('pending','accepted','blocked') NOT NULL DEFAULT 'pending',
  created_at    DATETIME  NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (requester_id, addressee_id),
  CONSTRAINT fk_friendship_requester
    FOREIGN KEY (requester_id) REFERENCES player (player_id)
    ON DELETE CASCADE,
  CONSTRAINT fk_friendship_addressee
    FOREIGN KEY (addressee_id) REFERENCES player (player_id)
    ON DELETE CASCADE,
  CONSTRAINT chk_friendship_not_self
    CHECK (requester_id <> addressee_id)
) ENGINE=InnoDB;

-- ---------------------------------------------------------------------
-- 5) Datos iniciales de level.
--    Deben existir ANTES de registrar jugadores, porque player.level_id
--    vale 1 por defecto y ese nivel tiene que existir.
-- ---------------------------------------------------------------------
INSERT IGNORE INTO level (level_number, level_name, xp_required, token_reward) VALUES
  (1, 'Freshman',     0,   0),
  (2, 'Sophomore',  100,  50),
  (3, 'Junior',     300, 100),
  (4, 'Senior',     600, 150),
  (5, 'Graduate',  1000, 250);

-- #####################################################################
-- MÓDULO 2 · TIENDA
-- #####################################################################

-- ---------------------------------------------------------------------
-- 1) cosmetic_category  ·  tabla madre (no depende de ninguna)
--    Los tipos de cosmético: atuendos, sombreros, mascotas, mochilas.
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS cosmetic_category (
  category_id    INT           NOT NULL AUTO_INCREMENT,
  category_name  VARCHAR(40)   NOT NULL,
  description    VARCHAR(255)  NULL,
  PRIMARY KEY (category_id),
  UNIQUE KEY uk_category_name (category_name)
) ENGINE=InnoDB;

-- ---------------------------------------------------------------------
-- 2) cosmetic_item  ·  depende de cosmetic_category
--    Cada artículo de la tienda.
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS cosmetic_item (
  item_id       INT           NOT NULL AUTO_INCREMENT,
  category_id   INT           NOT NULL,
  item_name     VARCHAR(60)   NOT NULL,
  description   VARCHAR(255)  NULL,
  rarity        ENUM('common','rare','epic','legendary') NOT NULL DEFAULT 'common',
  price_tokens  INT           NULL,
  asset_path    VARCHAR(255)  NULL,
  is_available  BOOLEAN       NOT NULL DEFAULT TRUE,
  PRIMARY KEY (item_id),
  UNIQUE KEY uk_item_name (item_name),
  CONSTRAINT fk_item_category
    FOREIGN KEY (category_id) REFERENCES cosmetic_category (category_id)
    ON DELETE RESTRICT
) ENGINE=InnoDB;

-- ---------------------------------------------------------------------
-- 3) player_inventory  ·  CONECTORA N:M entre player y cosmetic_item
--    Qué artículos tiene cada jugador y cuáles lleva puestos.
--    Sin id propio: su PK es la pareja (jugador, artículo).
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS player_inventory (
  player_id    INT       NOT NULL,
  item_id      INT       NOT NULL,
  acquired_at  DATETIME  NOT NULL DEFAULT CURRENT_TIMESTAMP,
  acquired_by  ENUM('purchase','achievement','gift') NOT NULL DEFAULT 'purchase',
  is_equipped  BOOLEAN   NOT NULL DEFAULT FALSE,
  PRIMARY KEY (player_id, item_id),
  CONSTRAINT fk_inventory_player
    FOREIGN KEY (player_id) REFERENCES player (player_id)
    ON DELETE CASCADE,
  CONSTRAINT fk_inventory_item
    FOREIGN KEY (item_id) REFERENCES cosmetic_item (item_id)
    ON DELETE RESTRICT
) ENGINE=InnoDB;

-- ---------------------------------------------------------------------
-- 4) Datos iniciales: las cuatro categorías de la tienda.
-- ---------------------------------------------------------------------
INSERT IGNORE INTO cosmetic_category (category_name, description) VALUES
  ('Atuendos',  'Batas, overoles y poleras de carrera'),
  ('Sombreros', 'Birretes, gorras y cascos de obra'),
  ('Mascotas',  'Acompañantes que siguen al jugador'),
  ('Mochilas',  'Mochilas y bolsos para la espalda');

-- #####################################################################
-- DATOS DE PRUEBA · ARTÍCULOS DE LA TIENDA
-- #####################################################################

INSERT IGNORE INTO cosmetic_item (category_id, item_name, description, rarity, price_tokens) VALUES
  ((SELECT category_id FROM cosmetic_category WHERE category_name = 'Atuendos'),
   'Bata de laboratorio', 'Blanca, con una mancha sospechosa.', 'common', 60),
  ((SELECT category_id FROM cosmetic_category WHERE category_name = 'Atuendos'),
   'Polera de la carrera', 'La que todos compran en primer semestre.', 'common', 40),
  ((SELECT category_id FROM cosmetic_category WHERE category_name = 'Atuendos'),
   'Toga de graduación', 'Solo para quien llega al final.', 'legendary', NULL),
  ((SELECT category_id FROM cosmetic_category WHERE category_name = 'Sombreros'),
   'Birrete', 'El clásico de toda graduación.', 'rare', 120),
  ((SELECT category_id FROM cosmetic_category WHERE category_name = 'Sombreros'),
   'Casco de obra', 'Obligatorio en la facultad de Ingeniería.', 'common', 50),
  ((SELECT category_id FROM cosmetic_category WHERE category_name = 'Mascotas'),
   'Perro de la facu', 'Nadie sabe de quién es, pero va a todas las clases.', 'epic', 250),
  ((SELECT category_id FROM cosmetic_category WHERE category_name = 'Mascotas'),
   'Paloma del patio', 'Te sigue si llevas salteñas.', 'rare', 150),
  ((SELECT category_id FROM cosmetic_category WHERE category_name = 'Mochilas'),
   'Mochila rota', 'Sobrevivió cinco semestres.', 'common', 30),
  ((SELECT category_id FROM cosmetic_category WHERE category_name = 'Mochilas'),
   'Portaplanos', 'Para planos... o para esconder evidencia.', 'rare', 110);
