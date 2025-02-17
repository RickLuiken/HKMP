﻿using Hkmp.Util;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace Hkmp.Animation.Effects {
    public abstract class SlashBase : DamageAnimationEffect {
        public abstract override void Play(GameObject playerObject, bool[] effectInfo);

        public override bool[] GetEffectInfo() {
            var playerData = PlayerData.instance;

            return new[] {
                playerData.GetInt(nameof(PlayerData.health)) == 1,
                playerData.GetInt(nameof(PlayerData.health)) == playerData.GetInt(nameof(PlayerData.maxHealth)),
                playerData.GetBool(nameof(PlayerData.equippedCharm_6)), // Fury of the fallen
                playerData.GetBool(nameof(PlayerData.equippedCharm_13)), // Mark of pride
                playerData.GetBool(nameof(PlayerData.equippedCharm_18)), // Long nail
                playerData.GetBool(nameof(PlayerData.equippedCharm_35)) // Grubberfly's Elegy
            };
        }

        protected void Play(GameObject playerObject, bool[] effectInfo, GameObject prefab, SlashType type) {
            // Read all needed information to do this effect from the packet
            var isOnOneHealth = effectInfo[0];
            var isOnFullHealth = effectInfo[1];
            var hasFuryCharm = effectInfo[2];
            var hasMarkOfPrideCharm = effectInfo[3];
            var hasLongNailCharm = effectInfo[4];
            var hasGrubberflyElegyCharm = effectInfo[5];

            // Get the attacks gameObject from the player object
            var playerAttacks = playerObject.FindGameObjectInChildren("Attacks");

            // Instantiate the slash gameObject from the given prefab
            // and use the attack gameObject as transform reference
            var slash = Object.Instantiate(prefab, playerAttacks.transform);
            // Get the NailSlash component and destroy it, since we don't want to interfere with the local player
            var originalNailSlash = slash.GetComponent<NailSlash>();
            Object.Destroy(originalNailSlash);

            ChangeAttackTypeOfFsm(slash);

            slash.SetActive(true);

            // Get the slash audio source and its clip
            var slashAudioSource = slash.GetComponent<AudioSource>();
            // Remove original audio source to prevent double audio
            Object.Destroy(slashAudioSource);
            var slashClip = slashAudioSource.clip;

            // Obtain the Nail Arts FSM from the Hero Controller
            var nailArts = HeroController.instance.gameObject.LocateMyFSM("Nail Arts");

            // Obtain the AudioSource from the AudioPlayerOneShotSingle action in the nail arts FSM
            var audioAction = nailArts.GetAction<AudioPlayerOneShotSingle>("Play Audio", 0);
            var audioPlayerObj = audioAction.audioPlayer.Value;
            var audioPlayer = audioPlayerObj.Spawn(playerObject.transform);
            var audioSource = audioPlayer.GetComponent<AudioSource>();

            // Play the slash clip with this newly spawned AudioSource
            audioSource.PlayOneShot(slashClip);

            // Store a boolean indicating whether the Fury of the fallen effect is active
            var fury = hasFuryCharm && isOnOneHealth;

            // If it is a wall slash, there is no scaling to do
            if (!type.Equals(SlashType.Wall)) {
                var scale = slash.transform.localScale;

                // Scale the nail slash based on Long nail and Mark of pride charms
                if (hasLongNailCharm) {
                    if (hasMarkOfPrideCharm) {
                        slash.transform.localScale = new Vector3(scale.x * 1.4f, scale.y * 1.4f,
                            scale.z);
                    } else {
                        slash.transform.localScale = new Vector3(scale.x * 1.25f,
                            scale.y * 1.25f,
                            scale.z);
                    }
                } else if (hasMarkOfPrideCharm) {
                    slash.transform.localScale = new Vector3(scale.x * 1.15f, scale.y * 1.15f,
                        scale.z);
                }
            }

            var slashAnimator = slash.GetComponent<tk2dSpriteAnimator>();
            // Figure out the name of the animation clip based on the slash type
            var clipName = "";
            // Down and Up prefixes
            if (type.Equals(SlashType.Down)) {
                clipName += "Down";
            }

            if (type.Equals(SlashType.Up)) {
                clipName += "Up";
            }

            // The body of the animation clip name
            clipName += "SlashEffect";

            // Alt suffix
            if (type.Equals(SlashType.Alt)) {
                clipName += "Alt";
            }

            // Prioritise fury and only play the Mark Of Pride animation clip if fury isn't active
            if (fury) {
                clipName += " F";
            } else if (hasMarkOfPrideCharm) {
                clipName += " M";
            }

            // Finally play the animation clip with the constructed name
            slashAnimator.PlayFromFrame(clipName, 0);

            slash.GetComponent<MeshRenderer>().enabled = true;

            var polygonCollider = slash.GetComponent<PolygonCollider2D>();

            polygonCollider.enabled = true;

            var damage = GameSettings.NailDamage;
            if (GameSettings.IsPvpEnabled && ShouldDoDamage && damage != 0) {
                // TODO: make it possible to pogo on players
                slash.AddComponent<DamageHero>().damageDealt = damage;
            }

            // After the animation is finished, we can destroy the slash object
            var animationDuration = slashAnimator.CurrentClip.Duration;
            Object.Destroy(slash, animationDuration);

            if (!hasGrubberflyElegyCharm
                || isOnOneHealth && !hasFuryCharm
                || !isOnFullHealth) {
                return;
            }

            GameObject elegyBeamPrefab;

            // Store a boolean indicating that we should take the fury variant of the beam prefab
            var furyVariant = isOnOneHealth;
            if (type.Equals(SlashType.Down)) {
                elegyBeamPrefab = furyVariant
                    ? HeroController.instance.grubberFlyBeamPrefabD_fury
                    : HeroController.instance.grubberFlyBeamPrefabD;
            } else if (type.Equals(SlashType.Up)) {
                elegyBeamPrefab = furyVariant
                    ? HeroController.instance.grubberFlyBeamPrefabU_fury
                    : HeroController.instance.grubberFlyBeamPrefabU;
            } else {
                var facingLeft = playerObject.transform.localScale.x > 0;

                if (facingLeft) {
                    elegyBeamPrefab = furyVariant
                        ? HeroController.instance.grubberFlyBeamPrefabL_fury
                        : HeroController.instance.grubberFlyBeamPrefabL;
                } else {
                    elegyBeamPrefab = furyVariant
                        ? HeroController.instance.grubberFlyBeamPrefabR_fury
                        : HeroController.instance.grubberFlyBeamPrefabR;
                }
            }

            // Instantiate the beam from the prefab with the playerObject position
            var elegyBeam = Object.Instantiate(
                elegyBeamPrefab,
                playerObject.transform.position,
                Quaternion.identity
            );

            elegyBeam.SetActive(true);
            elegyBeam.layer = 22;

            // Rotate the beam if it is an up or down slash
            var localScale = elegyBeam.transform.localScale;
            if (type.Equals(SlashType.Up) || type.Equals(SlashType.Down)) {
                elegyBeam.transform.localScale = new Vector3(
                    playerObject.transform.localScale.x,
                    localScale.y,
                    localScale.z
                );
                var z = 90;
                if (type.Equals(SlashType.Down) && playerObject.transform.localScale.x < 0) {
                    z = -90;
                }

                if (type.Equals(SlashType.Up) && playerObject.transform.localScale.x > 0) {
                    z = -90;
                }

                elegyBeam.transform.rotation = Quaternion.Euler(
                    0,
                    0,
                    z
                );
            }

            Object.Destroy(elegyBeam.LocateMyFSM("damages_enemy"));

            // If PvP is enabled, simply add a DamageHero component to the beam
            var elegyDamage = GameSettings.GrubberflyElegyDamage;
            if (GameSettings.IsPvpEnabled && ShouldDoDamage && elegyDamage != 0) {
                elegyBeam.AddComponent<DamageHero>().damageDealt = elegyDamage;
            }

            // We can destroy the elegy beam object after some time
            Object.Destroy(elegyBeam, 2.0f);
        }

        protected enum SlashType {
            Normal,
            Alt,
            Down,
            Up,
            Wall
        }
    }
}