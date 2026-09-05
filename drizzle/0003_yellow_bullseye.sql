CREATE TABLE "character_connections" (
	"character_id" integer PRIMARY KEY NOT NULL,
	"public_ip" varchar(16) NOT NULL,
	"public_port" integer NOT NULL,
	"private_ip" varchar(16) NOT NULL,
	"private_port" integer NOT NULL,
	"updated_at" timestamp with time zone DEFAULT now() NOT NULL
);
--> statement-breakpoint
ALTER TABLE "character_connections" ADD CONSTRAINT "character_connections_character_id_characters_id_fk" FOREIGN KEY ("character_id") REFERENCES "public"."characters"("id") ON DELETE cascade ON UPDATE no action;