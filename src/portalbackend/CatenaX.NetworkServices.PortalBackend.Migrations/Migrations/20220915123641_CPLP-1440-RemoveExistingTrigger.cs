﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatenaX.NetworkServices.PortalBackend.Migrations.Migrations
{
    public partial class CPLP1440RemoveExistingTrigger : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_audit_company_users_cplp_1254_db_audit_company_user_statuse",
                schema: "portal",
                table: "audit_company_users_cplp_1254_db_audit");

            migrationBuilder.DropIndex(
                name: "ix_audit_company_users_cplp_1254_db_audit_company_user_status_",
                schema: "portal",
                table: "audit_company_users_cplp_1254_db_audit");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS process_audit_company_users_audit();DROP TRIGGER audit_company_users ON  portal.company_users;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS process_audit_company_user_assigned_roles_audit(); DROP TRIGGER audit_company_user_assigned_roles ON portal.company_user_assigned_roles; ");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS process_audit_company_applications_audit(); DROP TRIGGER audit_company_applications ON portal.company_applications;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "CREATE OR REPLACE FUNCTION portal.process_company_users_audit() RETURNS TRIGGER AS $audit_company_users$ " +
                "BEGIN " +
                "IF (TG_OP = 'DELETE') THEN " +
                "INSERT INTO portal.audit_company_users_cplp_1254_db_audit ( id, audit_id, date_created,email,firstname,lastlogin,lastname,company_id,company_user_status_id, last_editor_id, date_last_changed, audit_operation_id ) SELECT gen_random_uuid(), OLD.id, OLD.date_created,OLD.email,OLD.firstname,OLD.lastlogin,OLD.lastname,OLD.company_id,OLD.company_user_status_id, OLD.last_editor_id, CURRENT_DATE, 3 ; " +
                "ELSIF (TG_OP = 'UPDATE') THEN " +
                "INSERT INTO portal.audit_company_users_cplp_1254_db_audit ( id, audit_id, date_created,email,firstname,lastlogin,lastname,company_id,company_user_status_id, last_editor_id, date_last_changed, audit_operation_id ) SELECT gen_random_uuid(), NEW.id, NEW.date_created,NEW.email,NEW.firstname,NEW.lastlogin,NEW.lastname,NEW.company_id,NEW.company_user_status_id, NEW.last_editor_id, CURRENT_DATE, 2 ; " +
                "ELSIF (TG_OP = 'INSERT') THEN " +
                "INSERT INTO portal.audit_company_users_cplp_1254_db_audit ( id, audit_id, date_created,email,firstname,lastlogin,lastname,company_id,company_user_status_id, last_editor_id, date_last_changed, audit_operation_id ) SELECT gen_random_uuid(), NEW.id, NEW.date_created,NEW.email,NEW.firstname,NEW.lastlogin,NEW.lastname,NEW.company_id,NEW.company_user_status_id, NEW.last_editor_id, CURRENT_DATE, 1 ; " +
                "END IF; " +
                "RETURN NULL; " +
                "END; " +
                "$audit_company_users$ LANGUAGE plpgsql; " +
                "CREATE OR REPLACE TRIGGER audit_company_users " +
                "AFTER INSERT OR UPDATE OR DELETE ON portal.company_users " +
                "FOR EACH ROW EXECUTE FUNCTION portal.process_company_users_audit();");
            
            migrationBuilder.Sql(
                "CREATE OR REPLACE FUNCTION portal.process_company_user_assigned_roles_audit() RETURNS TRIGGER AS $audit_company_user_assigned_roles$ " +
                "BEGIN " +
                "IF (TG_OP = 'DELETE') THEN " +
                "INSERT INTO portal.audit_company_user_assigned_roles_cplp_1255_audit_company_applications ( id, audit_id, company_user_id,user_role_id, last_editor_id, date_last_changed, audit_operation_id ) SELECT gen_random_uuid(), OLD.id, OLD.company_user_id,OLD.user_role_id, OLD.last_editor_id, CURRENT_DATE, 3 ; " +
                "ELSIF (TG_OP = 'UPDATE') THEN " +
                "INSERT INTO portal.audit_company_user_assigned_roles_cplp_1255_audit_company_applications ( id, audit_id, company_user_id,user_role_id, last_editor_id, date_last_changed, audit_operation_id ) SELECT gen_random_uuid(), NEW.id, NEW.company_user_id,NEW.user_role_id, NEW.last_editor_id, CURRENT_DATE, 2 ; " +
                "ELSIF (TG_OP = 'INSERT') THEN " +
                "INSERT INTO portal.audit_company_user_assigned_roles_cplp_1255_audit_company_applications ( id, audit_id, company_user_id,user_role_id, last_editor_id, date_last_changed, audit_operation_id ) SELECT gen_random_uuid(), NEW.id, NEW.company_user_id,NEW.user_role_id, NEW.last_editor_id, CURRENT_DATE, 1 ; " +
                "END IF; " +
                "RETURN NULL; " +
                "END; " +
                "$audit_company_user_assigned_roles$ LANGUAGE plpgsql; " +
                "CREATE OR REPLACE TRIGGER audit_company_user_assigned_roles " +
                "AFTER INSERT OR UPDATE OR DELETE ON portal.company_user_assigned_roles " +
                "FOR EACH ROW EXECUTE FUNCTION portal.process_company_user_assigned_roles_audit();");
            
            migrationBuilder.Sql(
                "CREATE OR REPLACE FUNCTION portal.process_company_applications_audit() RETURNS TRIGGER AS $audit_company_applications$ " +
                "BEGIN "+
                "IF (TG_OP = 'DELETE') THEN "+
                "INSERT INTO portal.audit_company_applications_cplp_1255_audit_company_applications ( id, audit_id, date_created,application_status_id,company_id, last_editor_id, date_last_changed, audit_operation_id ) SELECT gen_random_uuid(), OLD.id, OLD.date_created,OLD.application_status_id,OLD.company_id, OLD.last_editor_id, CURRENT_DATE, 3 ; "+
                "ELSIF (TG_OP = 'UPDATE') THEN "+
                "INSERT INTO portal.audit_company_applications_cplp_1255_audit_company_applications ( id, audit_id, date_created,application_status_id,company_id, last_editor_id, date_last_changed, audit_operation_id ) SELECT gen_random_uuid(), NEW.id, NEW.date_created,NEW.application_status_id,NEW.company_id, NEW.last_editor_id, CURRENT_DATE, 2 ; "+
                "ELSIF (TG_OP = 'INSERT') THEN "+
                "INSERT INTO portal.audit_company_applications_cplp_1255_audit_company_applications ( id, audit_id, date_created,application_status_id,company_id, last_editor_id, date_last_changed, audit_operation_id ) SELECT gen_random_uuid(), NEW.id, NEW.date_created,NEW.application_status_id,NEW.company_id, NEW.last_editor_id, CURRENT_DATE, 1 ; "+
                "END IF; "+
                "RETURN NULL; "+
                "END; "+
                "$audit_company_applications$ LANGUAGE plpgsql; "+
                "CREATE OR REPLACE TRIGGER audit_company_applications "+
                "AFTER INSERT OR UPDATE OR DELETE ON portal.company_applications "+
                "FOR EACH ROW EXECUTE FUNCTION portal.process_company_applications_audit(); ");
            
            migrationBuilder.CreateIndex(
                name: "ix_audit_company_users_cplp_1254_db_audit_company_user_status_",
                schema: "portal",
                table: "audit_company_users_cplp_1254_db_audit",
                column: "company_user_status_id");

            migrationBuilder.AddForeignKey(
                name: "fk_audit_company_users_cplp_1254_db_audit_company_user_statuse",
                schema: "portal",
                table: "audit_company_users_cplp_1254_db_audit",
                column: "company_user_status_id",
                principalSchema: "portal",
                principalTable: "company_user_statuses",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
