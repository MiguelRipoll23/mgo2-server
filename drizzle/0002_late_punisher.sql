CREATE TABLE "clan_applications" (
	"id" serial PRIMARY KEY NOT NULL,
	"clan_id" integer NOT NULL,
	"character_id" integer NOT NULL,
	"applied_at" timestamp DEFAULT now() NOT NULL,
	CONSTRAINT "clan_applications_unique" UNIQUE("clan_id","character_id")
);
--> statement-breakpoint
CREATE TABLE "gm_mail" (
	"id" serial PRIMARY KEY NOT NULL,
	"sender_character_id" integer,
	"sender_name" varchar(16) DEFAULT '' NOT NULL,
	"subject" varchar(128) DEFAULT '' NOT NULL,
	"body" varchar(708) DEFAULT '' NOT NULL,
	"sent_at" timestamp DEFAULT now() NOT NULL
);
--> statement-breakpoint
CREATE TABLE "mail" (
	"id" serial PRIMARY KEY NOT NULL,
	"sender_character_id" integer,
	"recipient_character_id" integer,
	"sender_name" varchar(16) DEFAULT '' NOT NULL,
	"recipient_name" varchar(16) DEFAULT '' NOT NULL,
	"subject" varchar(128) DEFAULT '' NOT NULL,
	"body" varchar(708) DEFAULT '' NOT NULL,
	"recipient_read" boolean DEFAULT false NOT NULL,
	"recipient_deleted" boolean DEFAULT false NOT NULL,
	"sender_read" boolean DEFAULT false NOT NULL,
	"sender_deleted" boolean DEFAULT false NOT NULL,
	"sent_at" timestamp DEFAULT now() NOT NULL
);
--> statement-breakpoint
CREATE TABLE "round_reports" (
	"id" serial PRIMARY KEY NOT NULL,
	"game_id" integer NOT NULL,
	"host_character_id" integer NOT NULL,
	"target_character_id" integer NOT NULL,
	"team_win" smallint DEFAULT 0 NOT NULL,
	"seconds" integer DEFAULT 0 NOT NULL,
	"experience" integer DEFAULT 0 NOT NULL,
	"aborted" boolean DEFAULT false NOT NULL,
	"lobby_subtype" smallint DEFAULT 0 NOT NULL,
	"created_at" timestamp DEFAULT now() NOT NULL
);
--> statement-breakpoint
CREATE TABLE "weapon_tallies" (
	"id" serial PRIMARY KEY NOT NULL,
	"game_id" integer NOT NULL,
	"character_id" integer NOT NULL,
	"weapon_id" smallint NOT NULL,
	"value_a" smallint DEFAULT 0 NOT NULL,
	"value_b" smallint DEFAULT 0 NOT NULL,
	"value_c" smallint DEFAULT 0 NOT NULL
);
--> statement-breakpoint
ALTER TABLE "clan_applications" ADD CONSTRAINT "clan_applications_clan_id_clans_id_fk" FOREIGN KEY ("clan_id") REFERENCES "public"."clans"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "clan_applications" ADD CONSTRAINT "clan_applications_character_id_characters_id_fk" FOREIGN KEY ("character_id") REFERENCES "public"."characters"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "gm_mail" ADD CONSTRAINT "gm_mail_sender_character_id_characters_id_fk" FOREIGN KEY ("sender_character_id") REFERENCES "public"."characters"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "mail" ADD CONSTRAINT "mail_sender_character_id_characters_id_fk" FOREIGN KEY ("sender_character_id") REFERENCES "public"."characters"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "mail" ADD CONSTRAINT "mail_recipient_character_id_characters_id_fk" FOREIGN KEY ("recipient_character_id") REFERENCES "public"."characters"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "round_reports" ADD CONSTRAINT "round_reports_game_id_games_id_fk" FOREIGN KEY ("game_id") REFERENCES "public"."games"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "round_reports" ADD CONSTRAINT "round_reports_host_character_id_characters_id_fk" FOREIGN KEY ("host_character_id") REFERENCES "public"."characters"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "round_reports" ADD CONSTRAINT "round_reports_target_character_id_characters_id_fk" FOREIGN KEY ("target_character_id") REFERENCES "public"."characters"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "weapon_tallies" ADD CONSTRAINT "weapon_tallies_game_id_games_id_fk" FOREIGN KEY ("game_id") REFERENCES "public"."games"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "weapon_tallies" ADD CONSTRAINT "weapon_tallies_character_id_characters_id_fk" FOREIGN KEY ("character_id") REFERENCES "public"."characters"("id") ON DELETE no action ON UPDATE no action;