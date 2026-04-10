# THROWING_VR

# VR Throwing Selection Data Collection Tool

This repository contains the Unity project used to collect behavioral data for a virtual reality throwing selection task.

## Overview

This project was developed to study how users perform target selection by throwing a virtual object in VR.  
Unlike conventional pointing, throwing is a ballistic interaction: once the object is released, the movement can no longer be corrected.  
The system was designed to present controlled target conditions and automatically record user behavior during each trial.

## Experimental Environment

- Unity3D (C#)
- Meta Quest 3
- Controller-based grab-and-release interaction
- Desktop PC for experiment execution

## Task

In each trial, the user grabs a virtual ball and throws it toward a target placed in front of them.  
The system manipulates:
- target width (`W`)
- movement amplitude (`A`)

The target appears only after the ball is grabbed.  
If the ball does not travel the required distance, the trial is repeated.

## Collected Data

The system records the following information during each trial:

- planning time (PT)
- collision endpoint `(x, y)`
- hit / miss outcome
- target width condition
- movement amplitude condition
- trial index and repetition
- 3D ball trajectory logs

PT is defined as the time between grabbing the ball and releasing it.  
The endpoint is defined as the collision point on the target plane (including miss trials).

## What This Dataset Can Be Used For

This dataset can be used for:

- analyzing user behavior in VR throwing-based target selection
- modeling planning time under different target conditions
- modeling endpoint distributions in ballistic interaction
- predicting selection accuracy from target parameters
- comparing the effects of target width and distance on performance
- studying motor variability in immersive interaction
- informing the design of VR interfaces and applications that use throwing interaction

Possible application domains include:
- VR games
- sports training
- rehabilitation
- immersive target-based interaction design

## Notes

The target conditions used in this project include multiple target widths and movement amplitudes.  
The collected data can support both behavioral analysis and predictive modeling.

## Data Availability

The dataset is not publicly hosted by default.  
De-identified data may be shared upon reasonable request.
