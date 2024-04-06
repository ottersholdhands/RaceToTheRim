﻿using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using RimWorld;

namespace RttRAnimalBehaviours
{
    public class JobDriver_IngestWeird : JobDriver
    {
        private Thing IngestibleSource
        {
            get
            {
                return this.job.GetTarget(TargetIndex.A).Thing;
            }
        }

        private float ChewDurationMultiplier
        {
            get
            {
               
                return 1f;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
          
        }

        public override string GetReport()
        {
           
            return base.GetReport();
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
           
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (this.pawn.Faction != null)
            {
                Thing ingestibleSource = this.IngestibleSource;
                if (!this.pawn.Reserve(ingestibleSource, this.job, 10, 1, null, errorOnFailed))
                {
                    return false;
                }
            }
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            CompEatWeirdFood comp = pawn.TryGetComp<CompEatWeirdFood>();
            if (comp == null)
            { yield break; }

            this.FailOn(() => this.IngestibleSource.Destroyed);

            Toil chew = ChewAnything(this.pawn, 1f, TargetIndex.A, TargetIndex.B).FailOn((Toil x) => !this.IngestibleSource.Spawned && (this.pawn.carryTracker == null || this.pawn.carryTracker.CarriedThing != this.IngestibleSource)).FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            foreach (Toil toil in this.PrepareToIngestToils(chew))
            {
                yield return toil;
            }
            
            yield return chew;
            yield return FinalizeIngestAnything(this.pawn, TargetIndex.A, comp);
            yield return Toils_Jump.JumpIf(chew, () => this.job.GetTarget(TargetIndex.A).Thing is Corpse && this.pawn.needs.food.CurLevelPercentage < 0.9f);
            yield break;
           
        }

        private IEnumerable<Toil> PrepareToIngestToils(Toil chewToil)
        {
          
            return this.PrepareToIngestToils_NonToolUser();
        }

      

        private IEnumerable<Toil> PrepareToIngestToils_NonToolUser()
        {
            yield return this.ReserveFood();
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield break;
        }

        private Toil ReserveFood()
        {
            return new Toil
            {
                initAction = delegate ()
                {
                    if (this.pawn.Faction == null)
                    {
                        return;
                    }
                    Thing thing = this.job.GetTarget(TargetIndex.A).Thing;
                    if (this.pawn.carryTracker.CarriedThing == thing)
                    {
                        return;
                    }
                    int maxAmountToPickup = 1;// FoodUtility.GetMaxAmountToPickup(thing, this.pawn, this.job.count);
                    if (maxAmountToPickup == 0)
                    {
                        return;
                    }
                    if (!this.pawn.Reserve(thing, this.job, 10, maxAmountToPickup, null, true))
                    {
                        Log.Error(string.Concat(new object[]
                        {
                            "Pawn food reservation for ",
                            this.pawn,
                            " on job ",
                            this,
                            " failed, because it could not register food from ",
                            thing,
                            " - amount: ",
                            maxAmountToPickup
                        }));
                        this.pawn.jobs.EndCurrentJob(JobCondition.Errored, true, true);
                    }
                    this.job.count = maxAmountToPickup;
                },
                defaultCompleteMode = ToilCompleteMode.Instant,
                atomicWithPrevious = true
            };
        }

        public static Toil ChewAnything(Pawn chewer, float durationMultiplier, TargetIndex ingestibleInd, TargetIndex eatSurfaceInd = TargetIndex.None)
        {
            Toil toil = new Toil();
            toil.initAction = delegate ()
            {
                Pawn actor = toil.actor;
                Thing thing = actor.CurJob.GetTarget(ingestibleInd).Thing;
                /* if (!thing.IngestibleNow)
                 {
                     chewer.jobs.EndCurrentJob(JobCondition.Incompletable, true, true);
                     return;
                 }*/
                actor.jobs.curDriver.ticksLeftThisToil = 500;
                if (thing.Spawned)
                {
                    thing.Map.physicalInteractionReservationManager.Reserve(chewer, actor.CurJob, thing);
                }
            };
            toil.tickAction = delegate ()
            {
                if (chewer != toil.actor)
                {
                    toil.actor.rotationTracker.FaceCell(chewer.Position);
                }
                else
                {
                    Thing thing = toil.actor.CurJob.GetTarget(ingestibleInd).Thing;
                    if (thing != null && thing.Spawned)
                    {
                        toil.actor.rotationTracker.FaceCell(thing.Position);
                    }
                    else if (eatSurfaceInd != TargetIndex.None && toil.actor.CurJob.GetTarget(eatSurfaceInd).IsValid)
                    {
                        toil.actor.rotationTracker.FaceCell(toil.actor.CurJob.GetTarget(eatSurfaceInd).Cell);
                    }
                }
                toil.actor.GainComfortFromCellIfPossible(false);
            };
            toil.WithProgressBar(ingestibleInd, delegate
            {
                Thing thing = toil.actor.CurJob.GetTarget(ingestibleInd).Thing;
                if (thing == null)
                {
                    return 1f;
                }
                return 1f - (float)toil.actor.jobs.curDriver.ticksLeftThisToil / Mathf.Round((float)500 * durationMultiplier);
            }, false, -0.5f);
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.FailOnDestroyedOrNull(ingestibleInd);
            toil.AddFinishAction(delegate
            {
                if (chewer == null)
                {
                    return;
                }
                if (chewer.CurJob == null)
                {
                    return;
                }
                Thing thing = chewer.CurJob.GetTarget(ingestibleInd).Thing;
                if (thing == null)
                {
                    return;
                }
                if (chewer.Map.physicalInteractionReservationManager.IsReservedBy(chewer, thing))
                {
                    chewer.Map.physicalInteractionReservationManager.Release(chewer, toil.actor.CurJob, thing);
                }
            });
            toil.handlingFacing = true;
            //Toils_Ingest.AddIngestionEffects(toil, chewer, ingestibleInd, eatSurfaceInd);
            return toil;
        }

        public static Toil FinalizeIngestAnything(Pawn ingester, TargetIndex ingestibleInd, CompEatWeirdFood comp)
        {
            Toil toil = new Toil();
            toil.initAction = delegate ()
            {
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                Thing thing = curJob.GetTarget(ingestibleInd).Thing;

                float num = ingester.needs.food.NutritionWanted;
                if (curJob.overeat)
                {
                    num = Mathf.Max(num, 0.75f);
                }
                float num2 = comp.Props.nutrition;// thing.Ingested(ingester, num);
                if (comp.Props.fullyDestroyThing)
                {
                    thing.Destroy(DestroyMode.Vanish);

                }
                else
                {
                    if (comp.Props.affectHitPoints)
                    {
                        thing.HitPoints -= (int)(thing.MaxHitPoints * comp.Props.percentageOfDestruction);
                        if (thing.HitPoints <= 0)
                        {
                            thing.Destroy(DestroyMode.Vanish);
                        }
                    }
                    else
                    {
                        int thingsToDestroy = (int)(comp.Props.percentageOfDestruction * thing.def.stackLimit);
                        //Log.Message(thingsToDestroy.ToString());

                        thing.stackCount = thing.stackCount - thingsToDestroy;
                        //Log.Message(thing.stackCount.ToString());
                        if (thing.stackCount < comp.Props.minStackToDestroy)
                        {
                            thing.Destroy(DestroyMode.Vanish);
                        }

                    }
                }

                if (comp.Props.hediffWhenEaten != "")
                {
                    actor.health.AddHediff(HediffDef.Named(comp.Props.hediffWhenEaten));
                }

                if (!ingester.Dead)
                {
                    ingester.needs.food.CurLevel += num2;
                }
                ingester.records.AddTo(RecordDefOf.NutritionEaten, num2);
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }





        public const float EatCorpseBodyPartsUntilFoodLevelPct = 0.9f;

        public const TargetIndex IngestibleSourceInd = TargetIndex.A;

        private const TargetIndex TableCellInd = TargetIndex.B;

        private const TargetIndex ExtraIngestiblesToCollectInd = TargetIndex.C;
    }
}

