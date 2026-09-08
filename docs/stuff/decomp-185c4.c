// function FUN_000185c4 @ 000185c4 (target 18800)

undefined8 FUN_000185c4(longlong *param_1)

{
  bool bVar1;
  undefined8 *puVar2;
  uint uVar3;
  int iVar4;
  int iVar5;
  int iVar6;
  undefined8 uVar7;
  int in_r7;
  int in_r8;
  undefined4 uVar8;
  uint uVar9;
  longlong lVar10;
  int *piVar11;
  undefined8 uVar12;
  undefined8 uVar13;
  int unaff_r29;
  undefined8 uVar14;
  undefined4 *unaff_r30;
  int *unaff_r31;
  int iVar15;
  int iVar16;
  byte in_cr7;
  undefined1 auStack_80 [64];
  
  if ((bool)(in_cr7 >> 1 & 1)) {
LAB_000187e0:
    iVar4 = 0x350 - in_r7;
    if (in_r7 == 0) {
      uVar9 = *(uint *)(unaff_r29 + 0x10);
      goto joined_r0x000187fc;
    }
  }
  else {
    in_r7 = (int)((ulonglong)(*param_1 * 4000) / 3000) * in_r8 + in_r7;
    *unaff_r31 = in_r7;
    if ((bool)(in_cr7 >> 3 & 1)) {
      iVar4 = 0x350 - in_r7;
      if (in_r7 < 1) {
        *unaff_r31 = 0;
        *unaff_r30 = 0;
        return 0;
      }
    }
    else {
      if (in_r7 < 0x96) goto LAB_000187e0;
      iVar4 = 0x2ba;
      *unaff_r31 = 0x96;
      in_r7 = 0x96;
      *unaff_r30 = 0;
    }
  }
  FUN_00012578(iVar4,0x19,900,in_r7,0x1e0,&DAT_000a0a0a);
  FUN_000125b0(0x350 - *unaff_r31,0x19,900,*unaff_r31,0x1e0,0x999999);
  uVar9 = *(uint *)(unaff_r29 + 0x10);
joined_r0x000187fc:
  if ((uVar9 & 0x100000) != 0) {
    iVar4 = *(int *)uRam001ec2a8;
    bVar1 = *(int *)uRam001ec278 != 0;
    do {
      while( true ) {
        do {
          if ((iVar4 == 0) && (iVar4 = 0x12, bVar1)) goto LAB_000186e0;
          iVar4 = iVar4 + -1;
          iVar5 = *(int *)((int)lRam001ec2b0 + iVar4 * 0x18);
        } while (iVar5 == 2);
        if (iVar5 == 1) break;
        if ((iVar5 != 5) || (bVar1)) goto LAB_000186e0;
      }
    } while (*(int *)uRam001ec268 == 0);
LAB_000186e0:
    *(int *)uRam001ec2a8 = iVar4;
  }
  if ((uVar9 & 0x400000) != 0) {
    iVar4 = *(int *)uRam001ec2a8;
    lVar10 = (ulonglong)(0x12 - iVar4) + 1;
LAB_00018730:
    lVar10 = lVar10 + -1;
    if (lVar10 != 0) goto LAB_00018738;
LAB_0001876c:
    iVar4 = 0;
LAB_00018770:
    *(int *)uRam001ec2a8 = iVar4;
  }
  uVar9 = *(uint *)(unaff_r29 + 8);
  uVar3 = FUN_00011308();
  if ((uVar3 & uVar9) == 0) {
    uVar9 = *(uint *)(unaff_r29 + 8);
    if ((uVar9 & 0x10) == 0) {
      uVar3 = FUN_000112e8();
      if ((uVar3 & uVar9) != 0) {
        FUN_0008ace8(uRam001ec2b8,0,0,0,0,0x3e9,0x70);
        return 1;
      }
      if (*unaff_r31 != 0x96) {
        return 1;
      }
      iVar16 = 0x28;
      iVar5 = FUN_00012b98(uRam001ec2c0);
      uVar9 = 0;
      uVar13 = 0xffffff;
      lVar10 = lRam001ec2b0 + 8;
      uVar12 = 0xff00;
      iVar4 = 0;
      do {
        puVar2 = (undefined8 *)lVar10;
        piVar11 = (int *)uRam001ec2a8;
        uVar14 = uVar13;
        if (uVar9 == 2) {
          iVar16 = iVar5 + iVar16;
          uVar14 = uVar12;
          if (*piVar11 != iVar4) {
LAB_000189d8:
            uVar14 = uVar13;
          }
LAB_000189dc:
          iVar6 = 0x2ce;
          FUN_000110c0(auStack_80,0x40,*puVar2);
          iVar15 = iVar16;
LAB_00018978:
          iVar16 = iVar5 + iVar15;
          FUN_00012640(iVar6,iVar15,800,uVar14,auStack_80);
        }
        else if (uVar9 == 1) {
          if (*(int *)uRam001ec268 != 0) {
            uVar14 = 0xffffff;
            if (*piVar11 == iVar4) {
              uVar14 = uVar12;
            }
            goto LAB_000189dc;
          }
        }
        else {
          if (uVar9 != 5) {
            if (*piVar11 == iVar4) {
              uVar14 = uVar12;
              if (uVar9 < 3) goto LAB_000189dc;
            }
            else if (uVar9 < 3) goto LAB_000189d8;
            piVar11 = (int *)uRam001ec260;
            iVar15 = iVar16;
            if (uVar9 == 3) {
              if (*piVar11 != *(int *)(puVar2 + 1)) {
                iVar6 = FUN_00012b58(uRam001ec2d0);
                iVar6 = iVar6 + 0x2ce;
                FUN_000110c0(auStack_80,0x40,*puVar2);
                goto LAB_00018978;
              }
              uVar7 = uRam001ec288;
              if (piVar11[1] == 0) {
                uVar7 = uRam001ec280;
              }
            }
            else {
              uVar7 = uRam001ec298;
              if (uVar9 == 4) {
                if ((piVar11[3] & *(uint *)(puVar2 + 1)) != 0) goto LAB_00018ad0;
              }
              else {
                if (uVar9 == 6) {
                  uVar7 = uRam001ec2a0;
                  if ((uint)*(byte *)((int)piVar11 + 0x11) == *(uint *)(puVar2 + 1)) {
                    uVar7 = *puVar2;
                  }
                  iVar6 = 0x2ce;
                  FUN_00010f70(auStack_80,0x40,uRam001ec2d8,uVar7);
                  goto LAB_00018978;
                }
                if (uVar9 == 8) {
                  if ((uint)*(byte *)((int)piVar11 + 0x12) == *(uint *)(puVar2 + 1)) {
LAB_00018ad0:
                    uVar7 = uRam001ec290;
                  }
                }
                else {
                  if (uVar9 != 7) {
                    iVar6 = 0x2ce;
                    if (uVar9 == 9) {
                      FUN_00010f70(auStack_80,0x40,uRam001ec2d8,
                                   *(undefined8 *)
                                    ((int)uRam001ec2e0 + (uint)*(byte *)(piVar11 + 2) * 0x18 + 8));
                    }
                    goto LAB_00018978;
                  }
                  if ((uint)*(byte *)(piVar11 + 4) == *(uint *)(puVar2 + 1)) goto LAB_00018ad0;
                }
              }
            }
            iVar6 = 0x2ce;
            FUN_00010f70(auStack_80,0x40,uRam001ec2c8,uVar7,*puVar2);
            goto LAB_00018978;
          }
          if (*(int *)uRam001ec278 != 0) {
            iVar16 = iVar5 + iVar16;
            if (*piVar11 == iVar4) {
              uVar14 = uVar12;
            }
            goto LAB_000189dc;
          }
        }
        iVar4 = iVar4 + 1;
        lVar10 = lVar10 + 0x18;
        if (iVar4 == 0x13) {
          return 1;
        }
        uVar9 = *(uint *)(puVar2 + 2);
      } while( true );
    }
    uVar8 = 2;
  }
  else {
    uVar8 = 3;
  }
  *(undefined4 *)uRam001ec258 = uVar8;
  *unaff_r30 = 0xffffffff;
  return 1;
LAB_00018738:
  while( true ) {
    iVar4 = iVar4 + 1;
    iVar5 = *(int *)((int)lRam001ec2b0 + iVar4 * 0x18);
    if (iVar5 == 2) break;
    if (iVar5 == 1) {
      if (*(int *)uRam001ec268 != 0) goto LAB_00018770;
      break;
    }
    if ((iVar5 != 5) || (*(int *)uRam001ec278 != 0)) goto LAB_00018770;
    lVar10 = lVar10 + -1;
    if (lVar10 == 0) goto LAB_0001876c;
  }
  goto LAB_00018730;
}

