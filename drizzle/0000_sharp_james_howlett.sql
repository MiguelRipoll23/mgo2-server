CREATE TABLE "characters_appearance" (
	"id" serial PRIMARY KEY NOT NULL,
	"character_id" integer NOT NULL,
	"gender" integer DEFAULT 0 NOT NULL,
	"face" integer DEFAULT 0 NOT NULL,
	"voice" integer DEFAULT 0 NOT NULL,
	"pitch" integer DEFAULT 0 NOT NULL,
	"head" integer DEFAULT 0 NOT NULL,
	"head_color" integer DEFAULT 0 NOT NULL,
	"upper" integer DEFAULT 0 NOT NULL,
	"upper_color" integer DEFAULT 0 NOT NULL,
	"lower" integer DEFAULT 0 NOT NULL,
	"lower_color" integer DEFAULT 0 NOT NULL,
	"chest" integer DEFAULT 0 NOT NULL,
	"chest_color" integer DEFAULT 0 NOT NULL,
	"waist" integer DEFAULT 0 NOT NULL,
	"waist_color" integer DEFAULT 0 NOT NULL,
	"hands" integer DEFAULT 0 NOT NULL,
	"hands_color" integer DEFAULT 0 NOT NULL,
	"feet" integer DEFAULT 0 NOT NULL,
	"feet_color" integer DEFAULT 0 NOT NULL,
	"accessory1" integer DEFAULT 0 NOT NULL,
	"accessory1_color" integer DEFAULT 0 NOT NULL,
	"accessory2" integer DEFAULT 0 NOT NULL,
	"accessory2_color" integer DEFAULT 0 NOT NULL,
	"face_paint" integer DEFAULT 0 NOT NULL,
	CONSTRAINT "characters_appearance_character_id_unique" UNIQUE("character_id")
);
--> statement-breakpoint
CREATE TABLE "characters_chatmacros" (
	"id" serial PRIMARY KEY NOT NULL,
	"character_id" integer NOT NULL,
	"type" integer DEFAULT 0 NOT NULL,
	"idx" integer DEFAULT 0 NOT NULL,
	"text" varchar(64) DEFAULT '' NOT NULL
);
--> statement-breakpoint
CREATE TABLE "character_connections" (
	"character_id" integer PRIMARY KEY NOT NULL,
	"public_ip" varchar(16) NOT NULL,
	"public_port" integer NOT NULL,
	"private_ip" varchar(16) NOT NULL,
	"private_port" integer NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
CREATE TABLE "characters_friends" (
	"id" serial PRIMARY KEY NOT NULL,
	"character_id" integer NOT NULL,
	"target_id" integer NOT NULL,
	"type" integer DEFAULT 0 NOT NULL
);
--> statement-breakpoint
CREATE TABLE "characters_hostsettings" (
	"id" serial PRIMARY KEY NOT NULL,
	"character_id" integer NOT NULL,
	"type" integer NOT NULL,
	"settings" text NOT NULL
);
--> statement-breakpoint
CREATE TABLE "characters_sets_gear" (
	"id" serial PRIMARY KEY NOT NULL,
	"character_id" integer NOT NULL,
	"idx" integer DEFAULT 0 NOT NULL,
	"name" varchar(63) DEFAULT '' NOT NULL,
	"stages" integer DEFAULT 0 NOT NULL,
	"face" integer DEFAULT 0 NOT NULL,
	"head" integer DEFAULT 0 NOT NULL,
	"head_color" integer DEFAULT 0 NOT NULL,
	"upper" integer DEFAULT 0 NOT NULL,
	"upper_color" integer DEFAULT 0 NOT NULL,
	"lower" integer DEFAULT 0 NOT NULL,
	"lower_color" integer DEFAULT 0 NOT NULL,
	"chest" integer DEFAULT 0 NOT NULL,
	"chest_color" integer DEFAULT 0 NOT NULL,
	"waist" integer DEFAULT 0 NOT NULL,
	"waist_color" integer DEFAULT 0 NOT NULL,
	"hands" integer DEFAULT 0 NOT NULL,
	"hands_color" integer DEFAULT 0 NOT NULL,
	"feet" integer DEFAULT 0 NOT NULL,
	"feet_color" integer DEFAULT 0 NOT NULL,
	"accessory1" integer DEFAULT 0 NOT NULL,
	"accessory1_color" integer DEFAULT 0 NOT NULL,
	"accessory2" integer DEFAULT 0 NOT NULL,
	"accessory2_color" integer DEFAULT 0 NOT NULL,
	"face_paint" integer DEFAULT 0 NOT NULL
);
--> statement-breakpoint
CREATE TABLE "characters_sets_skills" (
	"id" serial PRIMARY KEY NOT NULL,
	"character_id" integer NOT NULL,
	"idx" integer DEFAULT 0 NOT NULL,
	"name" varchar(63) DEFAULT '' NOT NULL,
	"modes" integer DEFAULT 0 NOT NULL,
	"skill_1" integer DEFAULT 0 NOT NULL,
	"skill_2" integer DEFAULT 0 NOT NULL,
	"skill_3" integer DEFAULT 0 NOT NULL,
	"skill_4" integer DEFAULT 0 NOT NULL,
	"level_1" integer DEFAULT 0 NOT NULL,
	"level_2" integer DEFAULT 0 NOT NULL,
	"level_3" integer DEFAULT 0 NOT NULL,
	"level_4" integer DEFAULT 0 NOT NULL
);
--> statement-breakpoint
CREATE TABLE "character_skills" (
	"character_id" integer NOT NULL,
	"skill_id" smallint NOT NULL,
	"experience" integer DEFAULT 0 NOT NULL,
	"flag" smallint DEFAULT 0 NOT NULL,
	CONSTRAINT "character_skills_character_id_skill_id_pk" PRIMARY KEY("character_id","skill_id")
);
--> statement-breakpoint
CREATE TABLE "character_stats" (
	"id" serial PRIMARY KEY NOT NULL,
	"character_id" integer NOT NULL,
	"kills" integer DEFAULT 0 NOT NULL,
	"deaths" integer DEFAULT 0 NOT NULL,
	"wins" integer DEFAULT 0 NOT NULL,
	"score" integer DEFAULT 0 NOT NULL,
	"rounds" integer DEFAULT 0 NOT NULL,
	"stuns" integer DEFAULT 0 NOT NULL,
	"stuns_received" integer DEFAULT 0 NOT NULL,
	"stuns_friendly" integer DEFAULT 0 NOT NULL,
	"headshot_kills" integer DEFAULT 0 NOT NULL,
	"headshot_deaths" integer DEFAULT 0 NOT NULL,
	"headshot_stuns" integer DEFAULT 0 NOT NULL,
	"headshot_stuns_received" integer DEFAULT 0 NOT NULL,
	"lock_kills" integer DEFAULT 0 NOT NULL,
	"lock_deaths" integer DEFAULT 0 NOT NULL,
	"lock_stuns" integer DEFAULT 0 NOT NULL,
	"lock_stuns_received" integer DEFAULT 0 NOT NULL,
	"consecutive_kills" integer DEFAULT 0 NOT NULL,
	"consecutive_deaths" integer DEFAULT 0 NOT NULL,
	"consecutive_headshots" integer DEFAULT 0 NOT NULL,
	"consecutive_tdm" integer DEFAULT 0 NOT NULL,
	"spotted" integer DEFAULT 0 NOT NULL,
	"self_spotted" integer DEFAULT 0 NOT NULL,
	"snake_spotted" integer DEFAULT 0 NOT NULL,
	"snake_self_spotted" integer DEFAULT 0 NOT NULL,
	"suicides" integer DEFAULT 0 NOT NULL,
	"salutes" integer DEFAULT 0 NOT NULL,
	"radio" integer DEFAULT 0 NOT NULL,
	"chat" integer DEFAULT 0 NOT NULL,
	"cqc_given" integer DEFAULT 0 NOT NULL,
	"cqc_taken" integer DEFAULT 0 NOT NULL,
	"rolls" integer DEFAULT 0 NOT NULL,
	"catapult" integer DEFAULT 0 NOT NULL,
	"falls" integer DEFAULT 0 NOT NULL,
	"trapped" integer DEFAULT 0 NOT NULL,
	"melee" integer DEFAULT 0 NOT NULL,
	"melee_rec" integer DEFAULT 0 NOT NULL,
	"box_time" integer DEFAULT 0 NOT NULL,
	"box_uses" integer DEFAULT 0 NOT NULL,
	"bases_captured" integer DEFAULT 0 NOT NULL,
	"bases_destroyed" integer DEFAULT 0 NOT NULL,
	"sop_destab" integer DEFAULT 0 NOT NULL,
	"gako_saved" integer DEFAULT 0 NOT NULL,
	"gako_defended" integer DEFAULT 0 NOT NULL,
	"gako_first" integer DEFAULT 0 NOT NULL,
	"res_defend" integer DEFAULT 0 NOT NULL,
	"res_gako_time" integer DEFAULT 0 NOT NULL,
	"res_first_grab" integer DEFAULT 0 NOT NULL,
	"bomb_disarms" integer DEFAULT 0 NOT NULL,
	"sdm_survivals" integer DEFAULT 0 NOT NULL,
	"race_checkpoints" integer DEFAULT 0 NOT NULL,
	"wins_snake" integer DEFAULT 0 NOT NULL,
	"kills_snake" integer DEFAULT 0 NOT NULL,
	"snake_holdups" integer DEFAULT 0 NOT NULL,
	"snake_tags_spawned" integer DEFAULT 0 NOT NULL,
	"snake_tags_taken" integer DEFAULT 0 NOT NULL,
	"snake_injured" integer DEFAULT 0 NOT NULL,
	"tsne_grab1" integer DEFAULT 0 NOT NULL,
	"tsne_grab2" integer DEFAULT 0 NOT NULL,
	"knife_kills" integer DEFAULT 0 NOT NULL,
	"knife_stuns" integer DEFAULT 0 NOT NULL,
	"boosts" integer DEFAULT 0 NOT NULL,
	"scans" integer DEFAULT 0 NOT NULL,
	"evg_time" integer DEFAULT 0 NOT NULL,
	"wakeups" integer DEFAULT 0 NOT NULL,
	"team_kills" integer DEFAULT 0 NOT NULL,
	"withdrawals" integer DEFAULT 0 NOT NULL,
	"points_assist" integer DEFAULT 0 NOT NULL,
	"points_base" integer DEFAULT 0 NOT NULL,
	"trained_soldiers" integer DEFAULT 0 NOT NULL,
	"time_training" integer DEFAULT 0 NOT NULL,
	"time_instructor" integer DEFAULT 0 NOT NULL,
	"time_student" integer DEFAULT 0 NOT NULL,
	"time" integer DEFAULT 0 NOT NULL,
	"time_snake" integer DEFAULT 0 NOT NULL,
	"time_dedi" integer DEFAULT 0 NOT NULL,
	"stats_dm" text,
	"stats_tdm" text,
	"stats_res" text,
	"stats_cap" text,
	"stats_base" text,
	"stats_bomb" text,
	"stats_sne" text,
	"stats_tsne" text,
	"stats_sdm" text,
	"stats_scap" text,
	"stats_race" text,
	"last_updated" integer,
	CONSTRAINT "character_stats_character_id_unique" UNIQUE("character_id")
);
--> statement-breakpoint
CREATE TABLE "characters" (
	"id" serial PRIMARY KEY NOT NULL,
	"user_id" integer NOT NULL,
	"name" varchar(16) NOT NULL,
	"old_name" varchar(16),
	"rank" integer DEFAULT 0 NOT NULL,
	"comment" varchar(128) DEFAULT '' NOT NULL,
	"host_score" integer DEFAULT 0 NOT NULL,
	"host_votes" integer DEFAULT 0 NOT NULL,
	"experience" integer DEFAULT 0 NOT NULL,
	"gameplay_options" text,
	"creation_time" integer DEFAULT 0 NOT NULL,
	"active" integer DEFAULT 1 NOT NULL,
	"lobby_id" integer,
	CONSTRAINT "characters_name_unique" UNIQUE("name")
);
--> statement-breakpoint
CREATE TABLE "clans_members" (
	"id" serial PRIMARY KEY NOT NULL,
	"clan_id" integer NOT NULL,
	"character_id" integer NOT NULL,
	"rank" integer DEFAULT 0 NOT NULL
);
--> statement-breakpoint
CREATE TABLE "clans" (
	"id" serial PRIMARY KEY NOT NULL,
	"name" varchar(15) NOT NULL,
	"leader_id" integer,
	"comment" varchar(128) DEFAULT '' NOT NULL,
	"notice" varchar(512) DEFAULT '' NOT NULL,
	"notice_time" integer DEFAULT 0 NOT NULL,
	"notice_writer_id" integer,
	"emblem_editor_id" integer,
	"emblem" "bytea",
	"emblem_wip" "bytea",
	"open" integer DEFAULT 1 NOT NULL,
	"created_at" timestamp DEFAULT now(),
	CONSTRAINT "clans_name_unique" UNIQUE("name")
);
--> statement-breakpoint
CREATE TABLE "game_players" (
	"game_id" integer NOT NULL,
	"character_id" integer NOT NULL,
	"team" smallint DEFAULT 0 NOT NULL,
	"ping" integer DEFAULT 0 NOT NULL,
	"joined_at" timestamp with time zone DEFAULT now() NOT NULL,
	CONSTRAINT "game_players_game_id_character_id_pk" PRIMARY KEY("game_id","character_id")
);
--> statement-breakpoint
CREATE TABLE "game_rounds" (
	"game_id" integer NOT NULL,
	"character_id" integer NOT NULL,
	CONSTRAINT "game_rounds_game_id_character_id_pk" PRIMARY KEY("game_id","character_id")
);
--> statement-breakpoint
CREATE TABLE "games" (
	"id" serial PRIMARY KEY NOT NULL,
	"host_id" integer NOT NULL,
	"lobby_id" integer NOT NULL,
	"name" varchar(16) NOT NULL,
	"password" varchar(15) DEFAULT '' NOT NULL,
	"comment" varchar(128) DEFAULT '' NOT NULL,
	"max_players" integer DEFAULT 8 NOT NULL,
	"current_game" integer DEFAULT 0 NOT NULL,
	"games" text DEFAULT '[]' NOT NULL,
	"stance" integer DEFAULT 0 NOT NULL,
	"ping" integer DEFAULT 0 NOT NULL,
	"common" text DEFAULT '{}' NOT NULL,
	"rules" text DEFAULT '{}' NOT NULL,
	"status" integer DEFAULT 0 NOT NULL,
	"created_at" timestamp DEFAULT now()
);
--> statement-breakpoint
CREATE TABLE "host_reviews" (
	"id" integer GENERATED BY DEFAULT AS IDENTITY (sequence name "host_reviews_id_seq" INCREMENT BY 1 MINVALUE 1 MAXVALUE 2147483647 START WITH 1 CACHE 1),
	"game_id" integer NOT NULL,
	"host_character_id" integer NOT NULL,
	"voter_character_id" integer NOT NULL,
	"rating" smallint NOT NULL,
	"reviewed_at" timestamp with time zone DEFAULT now() NOT NULL,
	CONSTRAINT "host_reviews_game_id_voter_character_id_pk" PRIMARY KEY("game_id","voter_character_id")
);
--> statement-breakpoint
CREATE TABLE "lobbies" (
	"id" serial PRIMARY KEY NOT NULL,
	"type_id" integer NOT NULL,
	"subtype_id" integer NOT NULL,
	"name" varchar(16) NOT NULL,
	"ip_address" varchar(15) NOT NULL,
	"port" integer NOT NULL,
	"players_count" integer DEFAULT 0 NOT NULL,
	"beginner_only" boolean DEFAULT false NOT NULL,
	"expansion_only" boolean DEFAULT false NOT NULL,
	"no_headshot" boolean DEFAULT false NOT NULL,
	"replays_only" boolean DEFAULT false NOT NULL,
	CONSTRAINT "lobbies_port_unique" UNIQUE("port")
);
--> statement-breakpoint
CREATE TABLE "lobby_game_types" (
	"id" serial PRIMARY KEY NOT NULL,
	"game_id" integer NOT NULL,
	"name" varchar(64) NOT NULL
);
--> statement-breakpoint
CREATE TABLE "lobby_instance_counts" (
	"instance_id" text NOT NULL,
	"lobby_id" integer NOT NULL,
	"count" integer DEFAULT 0 NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL,
	CONSTRAINT "lobby_instance_counts_instance_id_lobby_id_pk" PRIMARY KEY("instance_id","lobby_id")
);
--> statement-breakpoint
CREATE TABLE "news" (
	"id" serial PRIMARY KEY NOT NULL,
	"important" boolean DEFAULT false NOT NULL,
	"time" integer NOT NULL,
	"topic" varchar(128) NOT NULL,
	"message" text NOT NULL
);
--> statement-breakpoint
CREATE TABLE "sessions" (
	"id" serial PRIMARY KEY NOT NULL,
	"user_id" integer NOT NULL,
	"token" varchar(32) NOT NULL
);
--> statement-breakpoint
CREATE TABLE "users" (
	"id" serial PRIMARY KEY NOT NULL,
	"display_name" varchar(32) NOT NULL,
	"password" varchar(255) NOT NULL,
	"role" integer DEFAULT 0 NOT NULL,
	"banned_until" integer,
	"ban_reason" varchar(255),
	"slots" integer DEFAULT 3 NOT NULL,
	"current_character_id" integer,
	"main_character_id" integer,
	"main_exp" integer DEFAULT 0 NOT NULL,
	"alt_exp" integer DEFAULT 0 NOT NULL,
	CONSTRAINT "users_display_name_unique" UNIQUE("display_name")
);
--> statement-breakpoint
ALTER TABLE "characters_appearance" ADD CONSTRAINT "characters_appearance_character_id_characters_id_fk" FOREIGN KEY ("character_id") REFERENCES "public"."characters"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "characters_chatmacros" ADD CONSTRAINT "characters_chatmacros_character_id_characters_id_fk" FOREIGN KEY ("character_id") REFERENCES "public"."characters"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "character_connections" ADD CONSTRAINT "character_connections_character_id_characters_id_fk" FOREIGN KEY ("character_id") REFERENCES "public"."characters"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "characters_friends" ADD CONSTRAINT "characters_friends_character_id_characters_id_fk" FOREIGN KEY ("character_id") REFERENCES "public"."characters"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "characters_friends" ADD CONSTRAINT "characters_friends_target_id_characters_id_fk" FOREIGN KEY ("target_id") REFERENCES "public"."characters"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "characters_hostsettings" ADD CONSTRAINT "characters_hostsettings_character_id_characters_id_fk" FOREIGN KEY ("character_id") REFERENCES "public"."characters"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "characters_sets_gear" ADD CONSTRAINT "characters_sets_gear_character_id_characters_id_fk" FOREIGN KEY ("character_id") REFERENCES "public"."characters"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "characters_sets_skills" ADD CONSTRAINT "characters_sets_skills_character_id_characters_id_fk" FOREIGN KEY ("character_id") REFERENCES "public"."characters"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "character_skills" ADD CONSTRAINT "character_skills_character_id_characters_id_fk" FOREIGN KEY ("character_id") REFERENCES "public"."characters"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "character_stats" ADD CONSTRAINT "character_stats_character_id_characters_id_fk" FOREIGN KEY ("character_id") REFERENCES "public"."characters"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "characters" ADD CONSTRAINT "characters_user_id_users_id_fk" FOREIGN KEY ("user_id") REFERENCES "public"."users"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "characters" ADD CONSTRAINT "characters_lobby_id_lobbies_id_fk" FOREIGN KEY ("lobby_id") REFERENCES "public"."lobbies"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "clans_members" ADD CONSTRAINT "clans_members_clan_id_clans_id_fk" FOREIGN KEY ("clan_id") REFERENCES "public"."clans"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "clans_members" ADD CONSTRAINT "clans_members_character_id_characters_id_fk" FOREIGN KEY ("character_id") REFERENCES "public"."characters"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "clans" ADD CONSTRAINT "clans_leader_id_clans_members_id_fk" FOREIGN KEY ("leader_id") REFERENCES "public"."clans_members"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "clans" ADD CONSTRAINT "clans_notice_writer_id_clans_members_id_fk" FOREIGN KEY ("notice_writer_id") REFERENCES "public"."clans_members"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "clans" ADD CONSTRAINT "clans_emblem_editor_id_clans_members_id_fk" FOREIGN KEY ("emblem_editor_id") REFERENCES "public"."clans_members"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "game_players" ADD CONSTRAINT "game_players_game_id_games_id_fk" FOREIGN KEY ("game_id") REFERENCES "public"."games"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "game_players" ADD CONSTRAINT "game_players_character_id_characters_id_fk" FOREIGN KEY ("character_id") REFERENCES "public"."characters"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "game_rounds" ADD CONSTRAINT "game_rounds_game_id_games_id_fk" FOREIGN KEY ("game_id") REFERENCES "public"."games"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "game_rounds" ADD CONSTRAINT "game_rounds_character_id_characters_id_fk" FOREIGN KEY ("character_id") REFERENCES "public"."characters"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "games" ADD CONSTRAINT "games_host_id_characters_id_fk" FOREIGN KEY ("host_id") REFERENCES "public"."characters"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "games" ADD CONSTRAINT "games_lobby_id_lobbies_id_fk" FOREIGN KEY ("lobby_id") REFERENCES "public"."lobbies"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "host_reviews" ADD CONSTRAINT "host_reviews_host_character_id_characters_id_fk" FOREIGN KEY ("host_character_id") REFERENCES "public"."characters"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "host_reviews" ADD CONSTRAINT "host_reviews_voter_character_id_characters_id_fk" FOREIGN KEY ("voter_character_id") REFERENCES "public"."characters"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "lobbies" ADD CONSTRAINT "lobbies_subtype_id_lobby_game_types_id_fk" FOREIGN KEY ("subtype_id") REFERENCES "public"."lobby_game_types"("id") ON DELETE restrict ON UPDATE cascade;--> statement-breakpoint
ALTER TABLE "lobby_instance_counts" ADD CONSTRAINT "lobby_instance_counts_lobby_id_lobbies_id_fk" FOREIGN KEY ("lobby_id") REFERENCES "public"."lobbies"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "users" ADD CONSTRAINT "users_current_character_id_characters_id_fk" FOREIGN KEY ("current_character_id") REFERENCES "public"."characters"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "users" ADD CONSTRAINT "users_main_character_id_characters_id_fk" FOREIGN KEY ("main_character_id") REFERENCES "public"."characters"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
CREATE INDEX "character_skills_skill_id_idx" ON "character_skills" USING btree ("skill_id");--> statement-breakpoint
CREATE INDEX "host_reviews_host_idx" ON "host_reviews" USING btree ("host_character_id","reviewed_at");