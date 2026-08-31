using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetService.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Reconciles the model with a schema change that already happened on the live database
    /// under a since-deleted migration (PR #136's gap-fill renamed Trucks -> Vehicles and added
    /// master-data columns; that PR's application code was later reverted, but a revert cannot
    /// undo an already-applied migration, and the migration file itself was deleted along with
    /// the rest of the gap-fill — see Truck.cs / FleetServiceDbContext for the full story).
    /// Guarded so it's a no-op on the live DB (already renamed) but still correctly provisions
    /// a genuinely fresh database (new tenant, local dev, CI) that only has the old Trucks shape.
    /// </summary>
    public partial class ReconcileVehiclesSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Trucks')
                       AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Vehicles')
                    THEN
                        ALTER TABLE ""Trips"" DROP CONSTRAINT IF EXISTS ""FK_Trips_Trucks_TruckId"";
                        ALTER TABLE ""Trucks"" DROP CONSTRAINT IF EXISTS ""PK_Trucks"";
                        ALTER TABLE ""Trucks"" RENAME TO ""Vehicles"";
                        ALTER TABLE ""Trips"" RENAME COLUMN ""TruckLocationLongitude"" TO ""VehicleLocationLongitude"";
                        ALTER TABLE ""Trips"" RENAME COLUMN ""TruckLocationLatitude"" TO ""VehicleLocationLatitude"";
                        ALTER TABLE ""Trips"" RENAME COLUMN ""TruckId"" TO ""VehicleId"";
                        ALTER INDEX IF EXISTS ""IX_Trips_TruckId"" RENAME TO ""IX_Trips_VehicleId"";
                        ALTER INDEX IF EXISTS ""IX_Trucks_LicensePlate"" RENAME TO ""IX_Vehicles_LicensePlate"";
                        ALTER TABLE ""Trips"" ADD COLUMN ""Purpose"" text NOT NULL DEFAULT '';
                        ALTER TABLE ""Vehicles"" ADD COLUMN ""AssetId"" text NULL;
                        ALTER TABLE ""Vehicles"" ADD COLUMN ""Capacity"" numeric NULL;
                        ALTER TABLE ""Vehicles"" ADD COLUMN ""Department"" text NULL;
                        ALTER TABLE ""Vehicles"" ADD COLUMN ""FuelBenchmarkKmPerLitre"" numeric NULL;
                        ALTER TABLE ""Vehicles"" ADD COLUMN ""InspectionExpiryDate"" timestamptz NULL;
                        ALTER TABLE ""Vehicles"" ADD COLUMN ""InsuranceExpiryDate"" timestamptz NULL;
                        ALTER TABLE ""Vehicles"" ADD COLUMN ""Make"" text NOT NULL DEFAULT '';
                        ALTER TABLE ""Vehicles"" ADD COLUMN ""Odometer"" numeric NOT NULL DEFAULT 0;
                        ALTER TABLE ""Vehicles"" ADD COLUMN ""Status"" character varying(20) NOT NULL DEFAULT '';
                        ALTER TABLE ""Vehicles"" ADD COLUMN ""Year"" integer NOT NULL DEFAULT 0;
                        ALTER TABLE ""Vehicles"" ADD CONSTRAINT ""PK_Vehicles"" PRIMARY KEY (""Id"");
                        ALTER TABLE ""Trips"" ADD CONSTRAINT ""FK_Trips_Vehicles_VehicleId"" FOREIGN KEY (""VehicleId"") REFERENCES ""Vehicles"" (""Id"") ON DELETE RESTRICT;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Vehicles')
                       AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Trucks')
                    THEN
                        ALTER TABLE ""Trips"" DROP CONSTRAINT IF EXISTS ""FK_Trips_Vehicles_VehicleId"";
                        ALTER TABLE ""Vehicles"" DROP CONSTRAINT IF EXISTS ""PK_Vehicles"";
                        ALTER TABLE ""Vehicles"" DROP COLUMN IF EXISTS ""Year"";
                        ALTER TABLE ""Vehicles"" DROP COLUMN IF EXISTS ""Status"";
                        ALTER TABLE ""Vehicles"" DROP COLUMN IF EXISTS ""Odometer"";
                        ALTER TABLE ""Vehicles"" DROP COLUMN IF EXISTS ""Make"";
                        ALTER TABLE ""Vehicles"" DROP COLUMN IF EXISTS ""InsuranceExpiryDate"";
                        ALTER TABLE ""Vehicles"" DROP COLUMN IF EXISTS ""InspectionExpiryDate"";
                        ALTER TABLE ""Vehicles"" DROP COLUMN IF EXISTS ""FuelBenchmarkKmPerLitre"";
                        ALTER TABLE ""Vehicles"" DROP COLUMN IF EXISTS ""Department"";
                        ALTER TABLE ""Vehicles"" DROP COLUMN IF EXISTS ""Capacity"";
                        ALTER TABLE ""Vehicles"" DROP COLUMN IF EXISTS ""AssetId"";
                        ALTER TABLE ""Trips"" DROP COLUMN IF EXISTS ""Purpose"";
                        ALTER INDEX IF EXISTS ""IX_Vehicles_LicensePlate"" RENAME TO ""IX_Trucks_LicensePlate"";
                        ALTER INDEX IF EXISTS ""IX_Trips_VehicleId"" RENAME TO ""IX_Trips_TruckId"";
                        ALTER TABLE ""Trips"" RENAME COLUMN ""VehicleId"" TO ""TruckId"";
                        ALTER TABLE ""Trips"" RENAME COLUMN ""VehicleLocationLatitude"" TO ""TruckLocationLatitude"";
                        ALTER TABLE ""Trips"" RENAME COLUMN ""VehicleLocationLongitude"" TO ""TruckLocationLongitude"";
                        ALTER TABLE ""Vehicles"" RENAME TO ""Trucks"";
                        ALTER TABLE ""Trucks"" ADD CONSTRAINT ""PK_Trucks"" PRIMARY KEY (""Id"");
                        ALTER TABLE ""Trips"" ADD CONSTRAINT ""FK_Trips_Trucks_TruckId"" FOREIGN KEY (""TruckId"") REFERENCES ""Trucks"" (""Id"") ON DELETE RESTRICT;
                    END IF;
                END $$;
            ");
        }
    }
}
