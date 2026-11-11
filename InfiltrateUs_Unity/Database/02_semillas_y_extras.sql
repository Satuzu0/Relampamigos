-- Infiltrate Us · semillas + extras (correr DESPUES de infiltrate_us_v1.sql)
-- phpMyAdmin > base infiltrate_us_v1 > Importar. Se puede correr varias veces sin duplicar.

SET NAMES utf8mb4;
USE infiltrate_us_v1;

-- Colores de jugador (los usa el lobby)
INSERT INTO color_palette (color_code, display_name, hex_code) VALUES
 ('red','Rojo','#C51111'), ('blue','Azul','#132ED1'), ('green','Verde','#117F2D'),
 ('pink','Rosa','#ED54BA'), ('orange','Naranja','#EF7D0E'), ('yellow','Amarillo','#F5F557'),
 ('black','Negro','#3F474E'), ('white','Blanco','#D6E0F0'), ('purple','Morado','#6B2FBB'),
 ('cyan','Cian','#38FEDC')
ON DUPLICATE KEY UPDATE display_name = VALUES(display_name), hex_code = VALUES(hex_code);

-- Niveles 2 al 8 (el 1 ya existe)
INSERT INTO level (level_number, level_name, xp_required, reward_currency_id, reward_amount)
SELECT n.lv, CONCAT('Nivel ', n.lv), n.xp, c.currency_id, n.rw
FROM (SELECT 2 lv, 100 xp, 50 rw UNION ALL SELECT 3, 250, 75 UNION ALL SELECT 4, 450, 100
      UNION ALL SELECT 5, 700, 150 UNION ALL SELECT 6, 1000, 200 UNION ALL SELECT 7, 1400, 250
      UNION ALL SELECT 8, 1900, 300) n
JOIN currency c ON c.currency_code = 'TOKEN'
ON DUPLICATE KEY UPDATE level_name = VALUES(level_name), xp_required = VALUES(xp_required), reward_amount = VALUES(reward_amount);

-- Personajes. Ninguno es "default" a proposito: el jugador nuevo no tiene personaje hasta que elige uno.
-- sprite_ref apunta a Assets/Resources/Personajes/<carpeta>
INSERT INTO game_character (character_code, character_name, career, description, sprite_ref, status, is_default) VALUES
 ('char_01','Verde','Ing. de Sistemas','Polera verde, lentes y un tatuaje en el brazo. Siempre con la laptop.','Personajes/char_01','active',0),
 ('char_02','Campera','Ciberseguridad','Campera acolchada negra, rizos y trencita. Calmado, nunca se apura.','Personajes/char_02','active',0),
 ('char_03','Buzo','Algoritmos','Buzo gris y cero drama. El primero en terminar las tareas.','Personajes/char_03','active',0),
 ('char_04','Gorra','Redes','Gorra roja hacia atrás y lentes oscuros. Sospechosamente tranquilo.','Personajes/char_04','active',0)
ON DUPLICATE KEY UPDATE character_name = VALUES(character_name), career = VALUES(career),
  description = VALUES(description), sprite_ref = VALUES(sprite_ref), status = 'active', is_default = 0;

-- Mapa de prueba (permite 1 jugador para probar solo)
INSERT INTO map (map_code, map_name, university_name, description, unity_scene_name, min_players, max_players)
VALUES ('map_demo','Campus Demo','UNIFRANZ','Mapa de prueba para el lobby.','MapaDemo',1,10)
ON DUPLICATE KEY UPDATE map_name = VALUES(map_name), min_players = 1, max_players = 10, is_active = 1;

-- Cosmeticos (icon_ref = Assets/Resources/Icons/<archivo>)
INSERT INTO cosmetic_item (category_id, item_code, item_name, description, rarity, unlock_method, icon_ref)
SELECT c.category_id, i.code, i.nm, i.ds, i.rar, 'shop', CONCAT('Icons/', i.code)
FROM (
 SELECT 'hat' cat,'hat_gorra_laft' code,'Gorra "LA FT"' nm,'Algodón premium. Negro/Cian. Talla ajustable.' ds,'legendary' rar
 UNION ALL SELECT 'hat','hat_gorra_roja','Gorra roja','Clásica, hacia atrás.','common'
 UNION ALL SELECT 'hat','hat_gorro_lana','Gorro de lana','Para las noches frías del campus.','common'
 UNION ALL SELECT 'skin','skin_polera_cian','Polera cian','Polera color cian neón.','rare'
 UNION ALL SELECT 'skin','skin_buzo_negro','Buzo negro','Buzo oscuro, modo sigilo.','common'
 UNION ALL SELECT 'backpack','backpack_negra','Mochila negra','Cabe la laptop y tres libros.','common'
 UNION ALL SELECT 'backpack','backpack_laft','Mochila "LA FT"','Edición limitada de la facultad.','epic'
 UNION ALL SELECT 'accessory','acc_lentes','Lentes redondos','Los lentes del programador.','common'
 UNION ALL SELECT 'accessory','acc_audifonos','Audífonos gamer','Con luces y todo.','rare'
 UNION ALL SELECT 'pet','pet_gato','Gato pixelado','Te sigue a todos lados.','epic'
 UNION ALL SELECT 'pet','pet_robot','Mini robot','Un bot que casi sabe programar.','legendary'
) i
JOIN cosmetic_category c ON c.category_code = i.cat
ON DUPLICATE KEY UPDATE item_name = VALUES(item_name), description = VALUES(description),
  rarity = VALUES(rarity), icon_ref = VALUES(icon_ref), is_active = 1;

-- Productos de tienda en TOKEN (precio segun rareza)
INSERT INTO shop_product (item_id, currency_id, price)
SELECT i.item_id, c.currency_id,
       CASE i.rarity WHEN 'common' THEN 100 WHEN 'rare' THEN 250 WHEN 'epic' THEN 500 ELSE 950 END
FROM cosmetic_item i
JOIN currency c ON c.currency_code = 'TOKEN'
WHERE i.unlock_method = 'shop'
ON DUPLICATE KEY UPDATE price = VALUES(price), is_active = 1;

-- Logros
INSERT INTO achievement (achievement_code, achievement_name, description, condition_type, target_value, reward_currency_id, reward_amount)
SELECT a.code, a.nm, a.ds, a.ct, a.tv, c.currency_id, a.rw
FROM (
 SELECT 'first_game' code,'Primera partida' nm,'Juega tu primera partida.' ds,'games_played' ct,1 tv,50 rw
 UNION ALL SELECT 'veteran','Veterano','Juega 25 partidas.','games_played',25,200
 UNION ALL SELECT 'winner','Ganador','Gana 5 partidas.','games_won',5,150
 UNION ALL SELECT 'worker','Estudiante aplicado','Completa 20 tareas.','tasks_completed',20,100
 UNION ALL SELECT 'spy','Infiltrado maestro','Gana 3 partidas como infiltrado.','infiltrator_wins',3,200
 UNION ALL SELECT 'detective','Buen ojo','Vota correctamente 5 veces.','correct_votes',5,150
 UNION ALL SELECT 'streak3','Constante','Inicia sesión 3 días seguidos.','login_streak',3,100
 UNION ALL SELECT 'level5','Nivel 5','Llega al nivel 5.','level_reached',5,250
) a
JOIN currency c ON c.currency_code = 'TOKEN'
ON DUPLICATE KEY UPDATE achievement_name = VALUES(achievement_name), description = VALUES(description),
  target_value = VALUES(target_value), reward_amount = VALUES(reward_amount), is_active = 1;

-- Procedimiento extra: elegir (o quitar) el personaje del jugador
DELIMITER //
DROP PROCEDURE IF EXISTS sp_select_character //
CREATE PROCEDURE sp_select_character(IN p_player_id INT UNSIGNED, IN p_character_id SMALLINT UNSIGNED)
BEGIN
  IF NOT EXISTS (SELECT 1 FROM player WHERE player_id = p_player_id AND account_status = 'active') THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'PLAYER_NOT_FOUND';
  END IF;
  IF p_character_id IS NOT NULL AND p_character_id > 0 THEN
    IF NOT EXISTS (SELECT 1 FROM game_character WHERE character_id = p_character_id AND status = 'active') THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'CHARACTER_NOT_AVAILABLE';
    END IF;
    UPDATE player_profile SET selected_character_id = p_character_id WHERE player_id = p_player_id;
  ELSE
    UPDATE player_profile SET selected_character_id = NULL WHERE player_id = p_player_id;
  END IF;
END //
DELIMITER ;

-- Usuario para Unity (rol restringido). En pruebas locales tambien sirve root sin clave.
CREATE USER IF NOT EXISTS 'unity_server'@'localhost' IDENTIFIED BY 'infiltrate123';
CREATE USER IF NOT EXISTS 'unity_server'@'127.0.0.1' IDENTIFIED BY 'infiltrate123';
GRANT infiltrate_game_server TO 'unity_server'@'localhost';
GRANT infiltrate_game_server TO 'unity_server'@'127.0.0.1';
SET DEFAULT ROLE infiltrate_game_server FOR 'unity_server'@'localhost';
SET DEFAULT ROLE infiltrate_game_server FOR 'unity_server'@'127.0.0.1';
