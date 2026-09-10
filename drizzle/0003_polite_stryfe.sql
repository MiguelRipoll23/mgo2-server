CREATE TABLE "characters_equipped_skills" (
	"character_id" integer NOT NULL,
	"skill_1" integer DEFAULT 0 NOT NULL,
	"skill_2" integer DEFAULT 0 NOT NULL,
	"skill_3" integer DEFAULT 0 NOT NULL,
	"skill_4" integer DEFAULT 0 NOT NULL,
	"level_1" integer DEFAULT 0 NOT NULL,
	"level_2" integer DEFAULT 0 NOT NULL,
	"level_3" integer DEFAULT 0 NOT NULL,
	"level_4" integer DEFAULT 0 NOT NULL,
	CONSTRAINT "characters_equipped_skills_character_id_unique" UNIQUE("character_id")
);
--> statement-breakpoint
ALTER TABLE "characters_equipped_skills" ADD CONSTRAINT "characters_equipped_skills_character_id_characters_id_fk" FOREIGN KEY ("character_id") REFERENCES "public"."characters"("id") ON DELETE no action ON UPDATE no action;