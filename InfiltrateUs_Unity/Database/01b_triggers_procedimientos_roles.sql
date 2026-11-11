-- Infiltrate Us · parte 2: triggers, procedimientos, roles y permisos
SET NAMES utf8mb4;
USE infiltrate_us_v1;

-- #####################################################################
-- TRIGGERS
-- #####################################################################

DELIMITER //

DROP TRIGGER IF EXISTS trg_player_ai //
CREATE TRIGGER trg_player_ai AFTER INSERT ON player FOR EACH ROW BEGIN 
  INSERT INTO player_profile (player_id, display_name, selected_character_id) VALUES (NEW.player_id, NEW.username, (SELECT character_id FROM game_character WHERE is_default = 1 AND status = 'active' LIMIT 1)); 
  INSERT INTO player_stats (player_id) VALUES (NEW.player_id); 
  INSERT INTO player_wallet (player_id, currency_id) SELECT NEW.player_id, currency_id FROM currency WHERE is_active = 1; 
END //

DROP TRIGGER IF EXISTS trg_room_member_bi //
CREATE TRIGGER trg_room_member_bi BEFORE INSERT ON room_member FOR EACH ROW BEGIN 
  DECLARE v_state VARCHAR(10); 
  DECLARE v_max   TINYINT UNSIGNED; 
  DECLARE v_count INT; 
  SELECT room_state, max_players INTO v_state, v_max FROM game_room WHERE room_id = NEW.room_id FOR UPDATE; 
  IF v_state IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'ROOM_NOT_FOUND'; END IF; 
  IF v_state <> 'waiting' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'ROOM_NOT_JOINABLE'; END IF; 
  SELECT COUNT(*) INTO v_count FROM room_member WHERE room_id = NEW.room_id; 
  IF v_count >= v_max THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'ROOM_FULL'; END IF; 
END //

DROP TRIGGER IF EXISTS trg_wallettx_bu //
CREATE TRIGGER trg_wallettx_bu BEFORE UPDATE ON wallet_transaction FOR EACH ROW BEGIN 
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LEDGER_IS_APPEND_ONLY'; 
END //

DROP TRIGGER IF EXISTS trg_wallettx_bd //
CREATE TRIGGER trg_wallettx_bd BEFORE DELETE ON wallet_transaction FOR EACH ROW BEGIN 
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'LEDGER_IS_APPEND_ONLY'; 
END //

DELIMITER ;

-- #####################################################################
-- PROCEDIMIENTOS ALMACENADOS
-- #####################################################################

DELIMITER //

DROP PROCEDURE IF EXISTS sp_register_player //
CREATE PROCEDURE sp_register_player(
  IN  p_username      VARCHAR(30), 
  IN  p_email         VARCHAR(254), 
  IN  p_password_hash VARCHAR(255), 
  OUT p_player_id     INT UNSIGNED
) 
BEGIN 
  INSERT INTO player (username, email, password_hash) VALUES (p_username, NULLIF(p_email, ''), p_password_hash); 
  SET p_player_id = LAST_INSERT_ID(); 
END //

DROP PROCEDURE IF EXISTS sp_get_login_data //
CREATE PROCEDURE sp_get_login_data(IN p_username VARCHAR(30)) 
BEGIN 
  SELECT player_id, password_hash, account_status, failed_login_count, locked_until FROM player WHERE username = p_username AND deleted_at IS NULL; 
END //

DROP PROCEDURE IF EXISTS sp_login_failed //
CREATE PROCEDURE sp_login_failed(IN p_player_id INT UNSIGNED, IN p_max_attempts TINYINT UNSIGNED, IN p_lock_seconds INT) 
BEGIN 
  UPDATE player SET locked_until = IF(failed_login_count + 1 >= p_max_attempts, NOW() + INTERVAL p_lock_seconds SECOND, locked_until), failed_login_count = IF(failed_login_count + 1 >= p_max_attempts, 0, failed_login_count + 1) WHERE player_id = p_player_id; 
END //

DROP PROCEDURE IF EXISTS sp_login_succeeded //
CREATE PROCEDURE sp_login_succeeded(
  IN  p_player_id      INT UNSIGNED, 
  IN  p_token_hash     CHAR(64), 
  IN  p_ttl_seconds    INT, 
  IN  p_client_ip      VARCHAR(45), 
  IN  p_client_version VARCHAR(30), 
  OUT p_session_id     BIGINT UNSIGNED
) 
BEGIN 
  DECLARE v_status VARCHAR(10); 
  DECLARE v_locked DATETIME; 
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END; 
  START TRANSACTION; 
  SELECT account_status, locked_until INTO v_status, v_locked FROM player WHERE player_id = p_player_id AND deleted_at IS NULL FOR UPDATE; 
  IF v_status IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'PLAYER_NOT_FOUND'; END IF; 
  IF v_status <> 'active' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'PLAYER_NOT_ACTIVE'; END IF; 
  IF v_locked IS NOT NULL AND v_locked > NOW() THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'ACCOUNT_LOCKED'; END IF; 
  UPDATE player SET failed_login_count = 0, locked_until = NULL, last_login_at = NOW() WHERE player_id = p_player_id; 
  INSERT INTO player_session (player_id, token_hash, expires_at, client_ip, client_version) VALUES (p_player_id, p_token_hash, NOW() + INTERVAL p_ttl_seconds SECOND, p_client_ip, p_client_version); 
  SET p_session_id = LAST_INSERT_ID(); 
  UPDATE player_stats SET login_streak = IF(last_login_date = CURDATE(), login_streak, IF(last_login_date = CURDATE() - INTERVAL 1 DAY, login_streak + 1, 1)), last_login_date = CURDATE() WHERE player_id = p_player_id; 
  COMMIT; 
END //

DROP PROCEDURE IF EXISTS sp_validate_session //
CREATE PROCEDURE sp_validate_session(IN p_token_hash CHAR(64)) 
BEGIN 
  UPDATE player_session SET last_seen_at = NOW() WHERE token_hash = p_token_hash AND revoked_at IS NULL AND expires_at > NOW(); 
  SELECT s.player_id FROM player_session s JOIN player p ON p.player_id = s.player_id WHERE s.token_hash = p_token_hash AND s.revoked_at IS NULL AND s.expires_at > NOW() AND p.account_status = 'active'; 
END //

DROP PROCEDURE IF EXISTS sp_logout //
CREATE PROCEDURE sp_logout(IN p_token_hash CHAR(64)) 
BEGIN 
  UPDATE player_session SET revoked_at = NOW() WHERE token_hash = p_token_hash AND revoked_at IS NULL; 
END //

DROP PROCEDURE IF EXISTS sp_wallet_apply_core //
CREATE PROCEDURE sp_wallet_apply_core(
  IN  p_player_id      INT UNSIGNED, 
  IN  p_currency_id    TINYINT UNSIGNED, 
  IN  p_amount         BIGINT, 
  IN  p_reason         VARCHAR(20), 
  IN  p_match_id       INT UNSIGNED, 
  IN  p_achievement_id SMALLINT UNSIGNED, 
  IN  p_key            VARCHAR(80), 
  OUT p_transaction_id BIGINT UNSIGNED, 
  OUT p_replayed       TINYINT
) 
BEGIN 
  DECLARE v_balance  BIGINT; 
  DECLARE v_existing BIGINT UNSIGNED; 
  IF p_amount = 0 OR p_key IS NULL OR p_key = '' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'INVALID_WALLET_OPERATION'; END IF; 
  INSERT INTO player_wallet (player_id, currency_id) VALUES (p_player_id, p_currency_id) ON DUPLICATE KEY UPDATE player_id = player_id; 
  SELECT balance INTO v_balance FROM player_wallet WHERE player_id = p_player_id AND currency_id = p_currency_id FOR UPDATE; 
  SET v_existing = (SELECT transaction_id FROM wallet_transaction WHERE player_id = p_player_id AND idempotency_key = p_key); 
  IF v_existing IS NOT NULL THEN 
    SET p_transaction_id = v_existing; 
    SET p_replayed = 1; 
  ELSE 
    IF v_balance + p_amount < 0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'INSUFFICIENT_FUNDS'; END IF; 
    UPDATE player_wallet SET balance = balance + p_amount WHERE player_id = p_player_id AND currency_id = p_currency_id; 
    INSERT INTO wallet_transaction (player_id, currency_id, amount, balance_after, reason, match_id, achievement_id, idempotency_key) VALUES (p_player_id, p_currency_id, p_amount, v_balance + p_amount, p_reason, p_match_id, p_achievement_id, p_key); 
    SET p_transaction_id = LAST_INSERT_ID(); 
    SET p_replayed = 0; 
  END IF; 
END //

DROP PROCEDURE IF EXISTS sp_wallet_apply //
CREATE PROCEDURE sp_wallet_apply(
  IN  p_player_id      INT UNSIGNED, 
  IN  p_currency_id    TINYINT UNSIGNED, 
  IN  p_amount         BIGINT, 
  IN  p_reason         VARCHAR(20), 
  IN  p_match_id       INT UNSIGNED, 
  IN  p_achievement_id SMALLINT UNSIGNED, 
  IN  p_key            VARCHAR(80), 
  OUT p_transaction_id BIGINT UNSIGNED, 
  OUT p_replayed       TINYINT
) 
BEGIN 
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END; 
  START TRANSACTION; 
  CALL sp_wallet_apply_core(p_player_id, p_currency_id, p_amount, p_reason, p_match_id, p_achievement_id, p_key, p_transaction_id, p_replayed); 
  COMMIT; 
END //

DROP PROCEDURE IF EXISTS sp_add_xp_core //
CREATE PROCEDURE sp_add_xp_core(IN p_player_id INT UNSIGNED, IN p_xp INT) 
BEGIN 
  DECLARE v_xp    INT; 
  DECLARE v_old   SMALLINT UNSIGNED; 
  DECLARE v_new   SMALLINT UNSIGNED; 
  DECLARE v_done  TINYINT DEFAULT 0; 
  DECLARE v_curr  TINYINT UNSIGNED; 
  DECLARE v_sum   BIGINT; 
  DECLARE v_tx    BIGINT UNSIGNED; 
  DECLARE v_rep   TINYINT; 
  DECLARE cur_rw CURSOR FOR SELECT reward_currency_id, SUM(reward_amount) FROM level WHERE level_number > v_old AND level_number <= v_new AND reward_amount > 0 GROUP BY reward_currency_id; 
  DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_done = 1; 
  IF p_xp > 0 THEN 
    SELECT xp, level_number INTO v_xp, v_old FROM player_profile WHERE player_id = p_player_id FOR UPDATE; 
    IF v_xp IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'PLAYER_NOT_FOUND'; END IF; 
    SET v_xp  = v_xp + p_xp; 
    SET v_new = GREATEST(v_old, COALESCE((SELECT MAX(level_number) FROM level WHERE xp_required <= v_xp), v_old)); 
    UPDATE player_profile SET xp = v_xp, level_number = v_new WHERE player_id = p_player_id; 
    IF v_new > v_old THEN 
      SET v_done = 0; 
      OPEN cur_rw; 
      rw_loop: LOOP 
        FETCH cur_rw INTO v_curr, v_sum; 
        IF v_done = 1 THEN LEAVE rw_loop; END IF; 
        CALL sp_wallet_apply_core(p_player_id, v_curr, v_sum, 'level_up', NULL, NULL, CONCAT('level:', v_new, ':c', v_curr), v_tx, v_rep); 
      END LOOP; 
      CLOSE cur_rw; 
    END IF; 
  END IF; 
END //

DROP PROCEDURE IF EXISTS sp_purchase_item //
CREATE PROCEDURE sp_purchase_item(
  IN  p_player_id   INT UNSIGNED, 
  IN  p_product_id  INT UNSIGNED, 
  IN  p_key         VARCHAR(80), 
  OUT p_purchase_id BIGINT UNSIGNED
) 
BEGIN 
  DECLARE v_item     INT UNSIGNED; 
  DECLARE v_curr     TINYINT UNSIGNED; 
  DECLARE v_price    INT; 
  DECLARE v_active   TINYINT; 
  DECLARE v_item_act TINYINT; 
  DECLARE v_from     DATETIME; 
  DECLARE v_until    DATETIME; 
  DECLARE v_status   VARCHAR(10); 
  DECLARE v_balance  BIGINT; 
  DECLARE v_tx       BIGINT UNSIGNED; 
  DECLARE v_rep      TINYINT; 
  DECLARE v_prev_product INT UNSIGNED; 
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END; 
  START TRANSACTION; 
  SELECT p.item_id, p.currency_id, p.price, p.is_active, p.available_from, p.available_until, i.is_active INTO v_item, v_curr, v_price, v_active, v_from, v_until, v_item_act FROM shop_product p JOIN cosmetic_item i ON i.item_id = p.item_id WHERE p.product_id = p_product_id LOCK IN SHARE MODE; 
  IF v_item IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'PRODUCT_NOT_FOUND'; END IF; 
  IF v_active = 0 OR v_item_act = 0 OR (v_from IS NOT NULL AND v_from > NOW()) OR (v_until IS NOT NULL AND v_until <= NOW()) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'PRODUCT_NOT_AVAILABLE'; END IF; 
  SET v_status = (SELECT account_status FROM player WHERE player_id = p_player_id); 
  IF v_status IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'PLAYER_NOT_FOUND'; END IF; 
  IF v_status <> 'active' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'PLAYER_NOT_ACTIVE'; END IF; 
  INSERT INTO player_wallet (player_id, currency_id) VALUES (p_player_id, v_curr) ON DUPLICATE KEY UPDATE player_id = player_id; 
  SELECT balance INTO v_balance FROM player_wallet WHERE player_id = p_player_id AND currency_id = v_curr FOR UPDATE; 
  SET v_tx = (SELECT transaction_id FROM wallet_transaction WHERE player_id = p_player_id AND idempotency_key = p_key); 
  IF v_tx IS NOT NULL THEN 
    SET p_purchase_id  = (SELECT purchase_id FROM shop_purchase WHERE wallet_transaction_id = v_tx); 
    SET v_prev_product = (SELECT product_id  FROM shop_purchase WHERE wallet_transaction_id = v_tx); 
    IF p_purchase_id IS NULL OR v_prev_product <> p_product_id THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'IDEMPOTENCY_KEY_REUSED'; END IF; 
  ELSE 
    IF EXISTS (SELECT 1 FROM player_inventory WHERE player_id = p_player_id AND item_id = v_item) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'ITEM_ALREADY_OWNED'; END IF; 
    CALL sp_wallet_apply_core(p_player_id, v_curr, -v_price, 'purchase', NULL, NULL, p_key, v_tx, v_rep); 
    INSERT INTO shop_purchase (player_id, product_id, item_id, currency_id, price_paid, wallet_transaction_id) VALUES (p_player_id, p_product_id, v_item, v_curr, v_price, v_tx); 
    SET p_purchase_id = LAST_INSERT_ID(); 
    INSERT INTO player_inventory (player_id, item_id, acquired_via) VALUES (p_player_id, v_item, 'purchase'); 
  END IF; 
  COMMIT; 
END //

DROP PROCEDURE IF EXISTS sp_grant_item //
CREATE PROCEDURE sp_grant_item(IN p_player_id INT UNSIGNED, IN p_item_id INT UNSIGNED, IN p_via VARCHAR(12)) 
BEGIN 
  INSERT INTO player_inventory (player_id, item_id, acquired_via) VALUES (p_player_id, p_item_id, p_via) ON DUPLICATE KEY UPDATE player_id = player_id; 
END //

DROP PROCEDURE IF EXISTS sp_equip_item //
CREATE PROCEDURE sp_equip_item(IN p_player_id INT UNSIGNED, IN p_item_id INT UNSIGNED) 
BEGIN 
  DECLARE v_category TINYINT UNSIGNED; 
  IF NOT EXISTS (SELECT 1 FROM player_inventory WHERE player_id = p_player_id AND item_id = p_item_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'ITEM_NOT_OWNED'; END IF; 
  SET v_category = (SELECT category_id FROM cosmetic_item WHERE item_id = p_item_id); 
  INSERT INTO player_equipment (player_id, category_id, item_id) VALUES (p_player_id, v_category, p_item_id) ON DUPLICATE KEY UPDATE item_id = VALUES(item_id), equipped_at = NOW(); 
END //

DROP PROCEDURE IF EXISTS sp_unequip_category //
CREATE PROCEDURE sp_unequip_category(IN p_player_id INT UNSIGNED, IN p_category_id TINYINT UNSIGNED) 
BEGIN 
  DELETE FROM player_equipment WHERE player_id = p_player_id AND category_id = p_category_id; 
END //

DROP PROCEDURE IF EXISTS sp_update_achievement_progress //
CREATE PROCEDURE sp_update_achievement_progress(
  IN  p_player_id      INT UNSIGNED, 
  IN  p_achievement_id SMALLINT UNSIGNED, 
  IN  p_progress       INT, 
  OUT p_unlocked       TINYINT
) 
BEGIN 
  DECLARE v_target INT; 
  DECLARE v_curr   TINYINT UNSIGNED; 
  DECLARE v_amount INT; 
  DECLARE v_item   INT UNSIGNED; 
  DECLARE v_prog   INT; 
  DECLARE v_when   DATETIME; 
  DECLARE v_tx     BIGINT UNSIGNED; 
  DECLARE v_rep    TINYINT; 
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END; 
  SET p_unlocked = 0; 
  START TRANSACTION; 
  SELECT target_value, reward_currency_id, reward_amount, reward_item_id INTO v_target, v_curr, v_amount, v_item FROM achievement WHERE achievement_id = p_achievement_id AND is_active = 1; 
  IF v_target IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'ACHIEVEMENT_NOT_FOUND'; END IF; 
  INSERT INTO player_achievement (player_id, achievement_id) VALUES (p_player_id, p_achievement_id) ON DUPLICATE KEY UPDATE player_id = player_id; 
  SELECT progress, unlocked_at INTO v_prog, v_when FROM player_achievement WHERE player_id = p_player_id AND achievement_id = p_achievement_id FOR UPDATE; 
  IF v_when IS NULL THEN 
    SET v_prog = GREATEST(v_prog, p_progress); 
    IF v_prog >= v_target THEN 
      UPDATE player_achievement SET progress = v_prog, unlocked_at = NOW() WHERE player_id = p_player_id AND achievement_id = p_achievement_id; 
      SET p_unlocked = 1; 
      IF v_amount > 0 AND v_curr IS NOT NULL THEN CALL sp_wallet_apply_core(p_player_id, v_curr, v_amount, 'achievement', NULL, p_achievement_id, CONCAT('achievement:', p_achievement_id), v_tx, v_rep); END IF; 
      IF v_item IS NOT NULL THEN INSERT INTO player_inventory (player_id, item_id, acquired_via) VALUES (p_player_id, v_item, 'achievement') ON DUPLICATE KEY UPDATE player_id = player_id; END IF; 
    ELSE 
      UPDATE player_achievement SET progress = v_prog WHERE player_id = p_player_id AND achievement_id = p_achievement_id; 
    END IF; 
  END IF; 
  COMMIT; 
END //

DROP PROCEDURE IF EXISTS sp_create_room //
CREATE PROCEDURE sp_create_room(
  IN  p_host_player_id INT UNSIGNED, 
  IN  p_map_id         SMALLINT UNSIGNED, 
  IN  p_mode_id        SMALLINT UNSIGNED, 
  IN  p_is_private     TINYINT, 
  IN  p_min_players    TINYINT UNSIGNED, 
  IN  p_max_players    TINYINT UNSIGNED, 
  IN  p_host_address   VARCHAR(45), 
  IN  p_host_port      SMALLINT UNSIGNED, 
  OUT p_room_id        INT UNSIGNED, 
  OUT p_room_code      CHAR(6)
) 
BEGIN 
  DECLARE v_status   VARCHAR(10); 
  DECLARE v_map_max  TINYINT UNSIGNED; 
  DECLARE v_tries    TINYINT DEFAULT 0; 
  DECLARE v_code     CHAR(6); 
  DECLARE v_alphabet CHAR(32) DEFAULT 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'; 
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END; 
  START TRANSACTION; 
  SET v_status = (SELECT account_status FROM player WHERE player_id = p_host_player_id); 
  IF v_status IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'PLAYER_NOT_FOUND'; END IF; 
  IF v_status <> 'active' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'PLAYER_NOT_ACTIVE'; END IF; 
  IF EXISTS (SELECT 1 FROM room_member WHERE player_id = p_host_player_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'ALREADY_IN_A_ROOM'; END IF; 
  SET v_map_max = (SELECT max_players FROM map WHERE map_id = p_map_id AND is_active = 1); 
  IF v_map_max IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'MAP_NOT_AVAILABLE'; END IF; 
  IF NOT EXISTS (SELECT 1 FROM game_mode WHERE mode_id = p_mode_id AND is_active = 1) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'MODE_NOT_AVAILABLE'; END IF; 
  IF p_max_players > v_map_max OR p_min_players < 1 OR p_min_players > p_max_players THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'INVALID_ROOM_CAPACITY'; END IF; 
  code_loop: LOOP 
    SET v_code = CONCAT(
      SUBSTRING(v_alphabet, 1 + FLOOR(RAND() * 32), 1), 
      SUBSTRING(v_alphabet, 1 + FLOOR(RAND() * 32), 1), 
      SUBSTRING(v_alphabet, 1 + FLOOR(RAND() * 32), 1), 
      SUBSTRING(v_alphabet, 1 + FLOOR(RAND() * 32), 1), 
      SUBSTRING(v_alphabet, 1 + FLOOR(RAND() * 32), 1), 
      SUBSTRING(v_alphabet, 1 + FLOOR(RAND() * 32), 1)
    ); 
    IF NOT EXISTS (SELECT 1 FROM game_room WHERE room_code = v_code) THEN LEAVE code_loop; END IF; 
    SET v_tries = v_tries + 1; 
    IF v_tries >= 20 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'ROOM_CODE_UNAVAILABLE'; END IF; 
  END LOOP; 
  INSERT INTO game_room (room_code, host_player_id, map_id, mode_id, is_private, min_players, max_players, host_address, host_port) VALUES (v_code, NULL, p_map_id, p_mode_id, p_is_private, p_min_players, p_max_players, p_host_address, p_host_port); 
  SET p_room_id = LAST_INSERT_ID(); 
  SET p_room_code = v_code; 
  INSERT INTO room_member (room_id, player_id) VALUES (p_room_id, p_host_player_id); 
  UPDATE game_room SET host_player_id = p_host_player_id WHERE room_id = p_room_id; 
  COMMIT; 
END //

DROP PROCEDURE IF EXISTS sp_join_room //
CREATE PROCEDURE sp_join_room(
  IN  p_room_code CHAR(6), 
  IN  p_player_id INT UNSIGNED, 
  IN  p_color_id  TINYINT UNSIGNED, 
  OUT p_room_id   INT UNSIGNED
) 
BEGIN 
  DECLARE v_room    INT UNSIGNED; 
  DECLARE v_current INT UNSIGNED; 
  DECLARE v_status  VARCHAR(10); 
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END; 
  START TRANSACTION; 
  SELECT room_id INTO v_room FROM game_room WHERE room_code = p_room_code FOR UPDATE; 
  IF v_room IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'ROOM_NOT_FOUND'; END IF; 
  SET v_status = (SELECT account_status FROM player WHERE player_id = p_player_id); 
  IF v_status IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'PLAYER_NOT_FOUND'; END IF; 
  IF v_status <> 'active' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'PLAYER_NOT_ACTIVE'; END IF; 
  SET v_current = (SELECT room_id FROM room_member WHERE player_id = p_player_id); 
  IF v_current IS NULL THEN 
    INSERT INTO room_member (room_id, player_id, color_id) VALUES (v_room, p_player_id, p_color_id); 
  ELSEIF v_current = v_room THEN 
    UPDATE room_member SET connection_status = 'connected', disconnected_at = NULL, last_seen_at = NOW() WHERE room_id = v_room AND player_id = p_player_id; 
  ELSE 
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'ALREADY_IN_ANOTHER_ROOM'; 
  END IF; 
  SET p_room_id = v_room; 
  COMMIT; 
END //

DROP PROCEDURE IF EXISTS sp_leave_room //
CREATE PROCEDURE sp_leave_room(IN p_player_id INT UNSIGNED) 
BEGIN 
  DECLARE v_room     INT UNSIGNED; 
  DECLARE v_host     INT UNSIGNED; 
  DECLARE v_new_host INT UNSIGNED; 
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END; 
  START TRANSACTION; 
  SET v_room = (SELECT room_id FROM room_member WHERE player_id = p_player_id); 
  IF v_room IS NOT NULL THEN 
    SELECT host_player_id INTO v_host FROM game_room WHERE room_id = v_room FOR UPDATE; 
    UPDATE match_participant mp JOIN game_match m ON m.match_id = mp.match_id SET mp.connection_status = 'abandoned', mp.disconnected_at = COALESCE(mp.disconnected_at, NOW()) WHERE mp.player_id = p_player_id AND m.room_id = v_room AND m.status = 'in_progress'; 
    IF v_host = p_player_id THEN 
      SET v_new_host = (SELECT player_id FROM room_member WHERE room_id = v_room AND player_id <> p_player_id ORDER BY (connection_status = 'connected') DESC, joined_at, player_id LIMIT 1); 
      IF v_new_host IS NULL THEN 
        UPDATE game_room SET host_player_id = NULL, room_state = 'closed', closed_at = NOW() WHERE room_id = v_room; 
        UPDATE game_match SET status = 'cancelled', ended_at = NOW() WHERE room_id = v_room AND status = 'in_progress'; 
      ELSE 
        UPDATE game_room SET host_player_id = v_new_host WHERE room_id = v_room; 
      END IF; 
    END IF; 
    DELETE FROM room_member WHERE room_id = v_room AND player_id = p_player_id; 
  END IF; 
  COMMIT; 
END //

DROP PROCEDURE IF EXISTS sp_close_room //
CREATE PROCEDURE sp_close_room(IN p_room_id INT UNSIGNED, IN p_by_player_id INT UNSIGNED) 
BEGIN 
  DECLARE v_host  INT UNSIGNED; 
  DECLARE v_state VARCHAR(10); 
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END; 
  START TRANSACTION; 
  SELECT host_player_id, room_state INTO v_host, v_state FROM game_room WHERE room_id = p_room_id FOR UPDATE; 
  IF v_state IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'ROOM_NOT_FOUND'; END IF; 
  IF v_state <> 'closed' THEN 
    IF v_host IS NULL OR v_host <> p_by_player_id THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'NOT_ROOM_HOST'; END IF; 
    UPDATE game_match SET status = 'cancelled', ended_at = NOW() WHERE room_id = p_room_id AND status = 'in_progress'; 
    UPDATE game_room SET host_player_id = NULL, room_state = 'closed', closed_at = NOW() WHERE room_id = p_room_id; 
    DELETE FROM room_member WHERE room_id = p_room_id; 
  END IF; 
  COMMIT; 
END //

DROP PROCEDURE IF EXISTS sp_set_connection //
CREATE PROCEDURE sp_set_connection(IN p_player_id INT UNSIGNED, IN p_connected TINYINT) 
BEGIN 
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END; 
  START TRANSACTION; 
  UPDATE room_member SET connection_status = IF(p_connected = 1, 'connected', 'disconnected'), disconnected_at = IF(p_connected = 1, NULL, COALESCE(disconnected_at, NOW())), last_seen_at = NOW() WHERE player_id = p_player_id; 
  UPDATE match_participant mp JOIN game_match m ON m.match_id = mp.match_id AND m.status = 'in_progress' SET mp.connection_status = IF(p_connected = 1, 'connected', 'disconnected'), mp.disconnected_at = IF(p_connected = 1, mp.disconnected_at, NOW()), mp.reconnected_at = IF(p_connected = 1, NOW(), mp.reconnected_at), mp.disconnect_count = mp.disconnect_count + IF(p_connected = 1, 0, 1) WHERE mp.player_id = p_player_id AND mp.connection_status <> 'abandoned' AND mp.connection_status <> IF(p_connected = 1, 'connected', 'disconnected'); 
  COMMIT; 
END //

DROP PROCEDURE IF EXISTS sp_expire_disconnected //
CREATE PROCEDURE sp_expire_disconnected(IN p_grace_seconds INT) 
BEGIN 
  DECLARE v_done   TINYINT DEFAULT 0; 
  DECLARE v_player INT UNSIGNED; 
  DECLARE cur_lobby CURSOR FOR SELECT m.player_id FROM room_member m JOIN game_room r ON r.room_id = m.room_id WHERE r.room_state = 'waiting' AND m.connection_status = 'disconnected' AND m.disconnected_at < NOW() - INTERVAL p_grace_seconds SECOND; 
  DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_done = 1; 
  UPDATE match_participant mp JOIN game_match m ON m.match_id = mp.match_id AND m.status = 'in_progress' SET mp.connection_status = 'abandoned' WHERE mp.connection_status = 'disconnected' AND mp.disconnected_at < NOW() - INTERVAL p_grace_seconds SECOND; 
  OPEN cur_lobby; 
  lobby_loop: LOOP 
    FETCH cur_lobby INTO v_player; 
    IF v_done = 1 THEN LEAVE lobby_loop; END IF; 
    CALL sp_leave_room(v_player); 
  END LOOP; 
  CLOSE cur_lobby; 
END //

DROP PROCEDURE IF EXISTS sp_start_match //
CREATE PROCEDURE sp_start_match(
  IN  p_room_id        INT UNSIGNED, 
  IN  p_host_player_id INT UNSIGNED, 
  OUT p_match_id       INT UNSIGNED
) 
BEGIN 
  DECLARE v_state VARCHAR(10); 
  DECLARE v_host  INT UNSIGNED; 
  DECLARE v_map   SMALLINT UNSIGNED; 
  DECLARE v_mode  SMALLINT UNSIGNED; 
  DECLARE v_min   TINYINT UNSIGNED; 
  DECLARE v_count INT; 
  DECLARE v_default_char SMALLINT UNSIGNED; 
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END; 
  START TRANSACTION; 
  SELECT room_state, host_player_id, map_id, mode_id, min_players INTO v_state, v_host, v_map, v_mode, v_min FROM game_room WHERE room_id = p_room_id FOR UPDATE; 
  IF v_state IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'ROOM_NOT_FOUND'; END IF; 
  IF v_state <> 'waiting' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'ROOM_NOT_JOINABLE'; END IF; 
  IF v_host IS NULL OR v_host <> p_host_player_id THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'NOT_ROOM_HOST'; END IF; 
  SET v_count = (SELECT COUNT(*) FROM room_member WHERE room_id = p_room_id AND connection_status = 'connected'); 
  IF v_count < v_min THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'NOT_ENOUGH_PLAYERS'; END IF; 
  SET v_default_char = (SELECT character_id FROM game_character WHERE is_default = 1 AND status = 'active' LIMIT 1); 
  IF EXISTS (SELECT 1 FROM room_member rm JOIN player_profile pp ON pp.player_id = rm.player_id WHERE rm.room_id = p_room_id AND rm.connection_status = 'connected' AND COALESCE(pp.selected_character_id, v_default_char) IS NULL) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'CHARACTER_NOT_AVAILABLE'; END IF; 
  INSERT INTO game_match (room_id, map_id, mode_id) VALUES (p_room_id, v_map, v_mode); 
  SET p_match_id = LAST_INSERT_ID(); 
  INSERT INTO match_participant (match_id, mode_id, player_id, character_id, color_id) SELECT p_match_id, v_mode, rm.player_id, COALESCE(pp.selected_character_id, v_default_char), rm.color_id FROM room_member rm JOIN player_profile pp ON pp.player_id = rm.player_id WHERE rm.room_id = p_room_id AND rm.connection_status = 'connected'; 
  UPDATE game_room SET room_state = 'playing' WHERE room_id = p_room_id; 
  COMMIT; 
END //

DROP PROCEDURE IF EXISTS sp_record_kill //
CREATE PROCEDURE sp_record_kill(
  IN  p_match_id  INT UNSIGNED, 
  IN  p_killer_id INT UNSIGNED, 
  IN  p_victim_id INT UNSIGNED, 
  IN  p_zone_id   INT UNSIGNED, 
  OUT p_kill_id   INT UNSIGNED
) 
BEGIN 
  DECLARE v_map    SMALLINT UNSIGNED; 
  DECLARE v_status VARCHAR(12); 
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END; 
  START TRANSACTION; 
  SELECT map_id, status INTO v_map, v_status FROM game_match WHERE match_id = p_match_id FOR UPDATE; 
  IF v_status IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'MATCH_NOT_FOUND'; END IF; 
  IF v_status <> 'in_progress' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'MATCH_NOT_IN_PROGRESS'; END IF; 
  UPDATE match_participant SET life_status = 'killed' WHERE match_id = p_match_id AND participant_id = p_victim_id AND life_status = 'alive'; 
  IF ROW_COUNT() = 0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'VICTIM_NOT_ALIVE'; END IF; 
  INSERT INTO match_kill (match_id, map_id, killer_id, victim_id, zone_id) VALUES (p_match_id, v_map, p_killer_id, p_victim_id, p_zone_id); 
  SET p_kill_id = LAST_INSERT_ID(); 
  COMMIT; 
END //

DROP PROCEDURE IF EXISTS sp_start_meeting //
CREATE PROCEDURE sp_start_meeting(
  IN  p_match_id    INT UNSIGNED, 
  IN  p_called_by   INT UNSIGNED, 
  IN  p_type        VARCHAR(12), 
  IN  p_reported_id INT UNSIGNED, 
  IN  p_zone_id     INT UNSIGNED, 
  OUT p_meeting_id  INT UNSIGNED
) 
BEGIN 
  DECLARE v_map    SMALLINT UNSIGNED; 
  DECLARE v_status VARCHAR(12); 
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END; 
  START TRANSACTION; 
  SELECT map_id, status INTO v_map, v_status FROM game_match WHERE match_id = p_match_id FOR UPDATE; 
  IF v_status IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'MATCH_NOT_FOUND'; END IF; 
  IF v_status <> 'in_progress' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'MATCH_NOT_IN_PROGRESS'; END IF; 
  IF EXISTS (SELECT 1 FROM match_meeting WHERE match_id = p_match_id AND ended_at IS NULL) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'MEETING_ALREADY_OPEN'; END IF; 
  INSERT INTO match_meeting (match_id, map_id, meeting_type, called_by_id, reported_id, zone_id) VALUES (p_match_id, v_map, p_type, p_called_by, p_reported_id, p_zone_id); 
  SET p_meeting_id = LAST_INSERT_ID(); 
  COMMIT; 
END //

DROP PROCEDURE IF EXISTS sp_cast_vote //
CREATE PROCEDURE sp_cast_vote(IN p_meeting_id INT UNSIGNED, IN p_voter_id INT UNSIGNED, IN p_target_id INT UNSIGNED) 
BEGIN 
  DECLARE v_match INT UNSIGNED; 
  DECLARE v_ended DATETIME; 
  DECLARE v_found INT UNSIGNED; 
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END; 
  START TRANSACTION; 
  SELECT meeting_id, match_id, ended_at INTO v_found, v_match, v_ended FROM match_meeting WHERE meeting_id = p_meeting_id LOCK IN SHARE MODE; 
  IF v_found IS NULL OR v_ended IS NOT NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'MEETING_NOT_OPEN'; END IF; 
  INSERT INTO match_vote (meeting_id, voter_id, match_id, target_id) VALUES (p_meeting_id, p_voter_id, v_match, p_target_id); 
  COMMIT; 
END //

DROP PROCEDURE IF EXISTS sp_end_meeting //
CREATE PROCEDURE sp_end_meeting(IN p_meeting_id INT UNSIGNED, IN p_ejected_id INT UNSIGNED) 
BEGIN 
  DECLARE v_match INT UNSIGNED; 
  DECLARE v_ended DATETIME; 
  DECLARE v_found INT UNSIGNED; 
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END; 
  START TRANSACTION; 
  SELECT meeting_id, match_id, ended_at INTO v_found, v_match, v_ended FROM match_meeting WHERE meeting_id = p_meeting_id FOR UPDATE; 
  IF v_found IS NULL OR v_ended IS NOT NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'MEETING_NOT_OPEN'; END IF; 
  UPDATE match_meeting SET ended_at = NOW(), ejected_id = p_ejected_id WHERE meeting_id = p_meeting_id; 
  IF p_ejected_id IS NOT NULL THEN 
    UPDATE match_participant SET life_status = 'ejected' WHERE match_id = v_match AND participant_id = p_ejected_id AND life_status = 'alive'; 
  END IF; 
  COMMIT; 
END //

DROP PROCEDURE IF EXISTS sp_finish_match //
CREATE PROCEDURE sp_finish_match(IN p_match_id INT UNSIGNED, IN p_winner_team_id TINYINT UNSIGNED, IN p_win_condition_id TINYINT UNSIGNED) 
BEGIN 
  DECLARE v_status    VARCHAR(12); 
  DECLARE v_room      INT UNSIGNED; 
  DECLARE v_cond_team TINYINT UNSIGNED; 
  DECLARE v_done      TINYINT DEFAULT 0; 
  DECLARE v_player    INT UNSIGNED; 
  DECLARE v_part      INT UNSIGNED; 
  DECLARE v_curr      TINYINT UNSIGNED; 
  DECLARE v_amount    BIGINT; 
  DECLARE v_xp        INT; 
  DECLARE v_tx        BIGINT UNSIGNED; 
  DECLARE v_rep       TINYINT; 
  DECLARE cur_xp CURSOR FOR SELECT player_id, xp_earned FROM match_participant WHERE match_id = p_match_id AND xp_earned > 0; 
  DECLARE cur_rw CURSOR FOR SELECT mp.player_id, r.participant_id, r.currency_id, r.amount FROM match_reward r JOIN match_participant mp ON mp.participant_id = r.participant_id WHERE r.match_id = p_match_id AND r.applied_at IS NULL; 
  DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_done = 1; 
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END; 
  START TRANSACTION; 
  SELECT status, room_id INTO v_status, v_room FROM game_match WHERE match_id = p_match_id FOR UPDATE; 
  IF v_status IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'MATCH_NOT_FOUND'; END IF; 
  IF v_status <> 'in_progress' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'MATCH_NOT_IN_PROGRESS'; END IF; 
  IF p_win_condition_id IS NOT NULL THEN 
    IF NOT EXISTS (SELECT 1 FROM win_condition WHERE win_condition_id = p_win_condition_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'WIN_CONDITION_NOT_FOUND'; END IF; 
    SET v_cond_team = (SELECT winning_team_id FROM win_condition WHERE win_condition_id = p_win_condition_id); 
    IF v_cond_team IS NOT NULL AND (p_winner_team_id IS NULL OR p_winner_team_id <> v_cond_team) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'WIN_CONDITION_TEAM_MISMATCH'; END IF; 
  END IF; 
  UPDATE game_match SET status = 'finished', ended_at = NOW(), winner_team_id = p_winner_team_id, win_condition_id = p_win_condition_id WHERE match_id = p_match_id; 
  UPDATE match_participant mp LEFT JOIN role r ON r.role_id = mp.role_id SET mp.is_winner = IF(p_winner_team_id IS NULL, 0, COALESCE(r.team_id = p_winner_team_id, 0)) WHERE mp.match_id = p_match_id; 
  UPDATE player_stats ps JOIN match_participant mp ON mp.player_id = ps.player_id AND mp.match_id = p_match_id LEFT JOIN role r ON r.role_id = mp.role_id LEFT JOIN team t ON t.team_id = r.team_id SET ps.games_played = ps.games_played + 1, ps.games_won = ps.games_won + IF(mp.is_winner = 1, 1, 0), ps.infiltrator_wins = ps.infiltrator_wins + IF(mp.is_winner = 1 AND t.is_adversary = 1, 1, 0); 
  UPDATE player_stats ps JOIN (SELECT mp.player_id, COUNT(*) AS c FROM match_kill k JOIN match_participant mp ON mp.participant_id = k.killer_id WHERE k.match_id = p_match_id GROUP BY mp.player_id) x ON x.player_id = ps.player_id SET ps.kills = ps.kills + x.c; 
  UPDATE player_stats ps JOIN (SELECT mp.player_id, COUNT(*) AS c FROM match_task mt JOIN match_participant mp ON mp.participant_id = mt.participant_id WHERE mt.match_id = p_match_id AND mt.completed_at IS NOT NULL GROUP BY mp.player_id) x ON x.player_id = ps.player_id SET ps.tasks_completed = ps.tasks_completed + x.c; 
  UPDATE player_stats ps JOIN (SELECT voter.player_id, COUNT(*) AS c FROM match_vote v JOIN match_participant voter ON voter.participant_id = v.voter_id JOIN match_participant tgt ON tgt.participant_id = v.target_id AND tgt.life_status = 'ejected' JOIN role tr ON tr.role_id = tgt.role_id JOIN team tt ON tt.team_id = tr.team_id AND tt.is_adversary = 1 WHERE v.match_id = p_match_id GROUP BY voter.player_id) x ON x.player_id = ps.player_id SET ps.correct_votes = ps.correct_votes + x.c; 
  SET v_done = 0; 
  OPEN cur_xp; 
  xp_loop: LOOP 
    FETCH cur_xp INTO v_player, v_xp; 
    IF v_done = 1 THEN LEAVE xp_loop; END IF; 
    CALL sp_add_xp_core(v_player, v_xp); 
  END LOOP; 
  CLOSE cur_xp; 
  SET v_done = 0; 
  OPEN cur_rw; 
  rw_loop: LOOP 
    FETCH cur_rw INTO v_player, v_part, v_curr, v_amount; 
    IF v_done = 1 THEN LEAVE rw_loop; END IF; 
    CALL sp_wallet_apply_core(v_player, v_curr, v_amount, 'match_reward', p_match_id, NULL, CONCAT('match:', p_match_id, ':p', v_part, ':c', v_curr), v_tx, v_rep); 
    UPDATE match_reward SET applied_at = NOW() WHERE match_id = p_match_id AND participant_id = v_part AND currency_id = v_curr; 
  END LOOP; 
  CLOSE cur_rw; 
  UPDATE game_room SET room_state = 'waiting' WHERE room_id = v_room AND room_state = 'playing'; 
  UPDATE room_member SET is_ready = 0 WHERE room_id = v_room; 
  COMMIT; 
END //

DROP PROCEDURE IF EXISTS sp_cancel_match //
CREATE PROCEDURE sp_cancel_match(IN p_match_id INT UNSIGNED) 
BEGIN 
  DECLARE v_status VARCHAR(12); 
  DECLARE v_room   INT UNSIGNED; 
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END; 
  START TRANSACTION; 
  SELECT status, room_id INTO v_status, v_room FROM game_match WHERE match_id = p_match_id FOR UPDATE; 
  IF v_status IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'MATCH_NOT_FOUND'; END IF; 
  IF v_status <> 'in_progress' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'MATCH_NOT_IN_PROGRESS'; END IF; 
  UPDATE game_match SET status = 'cancelled', ended_at = NOW() WHERE match_id = p_match_id; 
  UPDATE game_room SET room_state = 'waiting' WHERE room_id = v_room AND room_state = 'playing'; 
  UPDATE room_member SET is_ready = 0 WHERE room_id = v_room; 
  COMMIT; 
END //

DELIMITER ;

-- #####################################################################
-- SEGURIDAD Y ROLES DE BASE DE DATOS
-- #####################################################################

CREATE ROLE IF NOT EXISTS infiltrate_game_server;
CREATE ROLE IF NOT EXISTS infiltrate_admin;

GRANT ALL PRIVILEGES ON infiltrate_us_v1.* TO infiltrate_admin;

GRANT EXECUTE ON infiltrate_us_v1.* TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.currency TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.level TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.team TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.win_condition TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.role TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.ability TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.role_ability TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.game_mode TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.mode_role TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.color_palette TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.game_character TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.cosmetic_category TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.cosmetic_item TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.character_default_cosmetic TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.shop_product TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.player_inventory TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.player_equipment TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.map TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.zone_type TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.map_zone TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.map_location TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.map_camera TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.vent TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.vent_link TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.task_type TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.task_difficulty TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.task TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.map_task TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.sabotage_type TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.map_sabotage TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.achievement TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.player_achievement TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.player_wallet TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.game_room TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.room_member TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.game_match TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.match_participant TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.match_reward TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.match_task TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.match_sabotage TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.match_meeting TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.match_vote TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.match_kill TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.match_event TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.chat_message TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.v_player_public TO infiltrate_game_server;
GRANT SELECT ON infiltrate_us_v1.v_room_lobby TO infiltrate_game_server;
GRANT SELECT (player_id, username, account_status, created_at) ON infiltrate_us_v1.player TO infiltrate_game_server;
GRANT INSERT, UPDATE ON infiltrate_us_v1.match_task TO infiltrate_game_server;
GRANT INSERT, UPDATE ON infiltrate_us_v1.match_sabotage TO infiltrate_game_server;
GRANT INSERT, UPDATE ON infiltrate_us_v1.match_reward TO infiltrate_game_server;
GRANT INSERT ON infiltrate_us_v1.match_event TO infiltrate_game_server;
GRANT INSERT ON infiltrate_us_v1.chat_message TO infiltrate_game_server;
GRANT UPDATE (role_id, xp_earned) ON infiltrate_us_v1.match_participant TO infiltrate_game_server;
GRANT UPDATE (is_ready, color_id) ON infiltrate_us_v1.room_member TO infiltrate_game_server;

