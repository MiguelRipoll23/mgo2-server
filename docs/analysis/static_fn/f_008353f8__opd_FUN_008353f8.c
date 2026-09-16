/* containing function of static patch target(s); entry .opd.FUN_008353f8 @ 008353f8 */

/* WARNING: Heritage AFTER dead removal. Example location: w0xffffff80 : 0x00835f28 */
/* WARNING: Restarted to delay deadcode elimination for space: stack */

void _opd_FUN_008353f8(int param_1)

{
  float fVar1;
  float fVar2;
  float fVar3;
  float fVar4;
  float fVar5;
  float fVar6;
  float fVar7;
  float fVar8;
  float fVar9;
  float fVar10;
  undefined2 uVar11;
  int iVar12;
  int iVar13;
  int iVar14;
  int iVar15;
  float fVar16;
  float fVar17;
  float fVar18;
  float *pfVar19;
  undefined *puVar20;
  short sVar22;
  int iVar21;
  uint uVar24;
  ulonglong uVar23;
  double dVar25;
  undefined1 auVar26 [16];
  undefined1 local_100 [28];
  float fStack_e4;
  float local_e0;
  float fStack_dc;
  float local_d8;
  float local_90;
  float local_88;
  undefined1 auStack_80 [18];
  undefined2 uStack_6e;
  
  puVar20 = PTR_DAT_0121b88c;
  iVar12 = *(int *)(param_1 + 0x1c);
  iVar13 = *(int *)(iVar12 + 0x18);
  if (*(int *)(iVar12 + 0x824) < 0x45) {
    *(int *)(iVar12 + 0x828) = *(int *)(iVar12 + 0x824);
LAB_008354f4:
    iVar14 = *(int *)(iVar12 + 0x8c);
  }
  else {
    iVar14 = *(int *)(iVar12 + 0x828);
    if (((iVar14 != 0x13) && (iVar14 != 0x18)) && (iVar14 != 0x2cc)) goto LAB_008354f4;
    *(undefined4 *)(iVar12 + 0x828) = 1;
    iVar14 = *(int *)(iVar12 + 0x8c);
  }
  if ((iVar14 == 9) && ((*(int *)(iVar12 + 0x94) == 0x37 || (*(int *)(iVar12 + 0x94) == 0x38)))) {
    uVar11 = *(undefined2 *)(iVar12 + 0x820);
    *(undefined4 *)(iVar12 + 0x828) = 1;
    *(undefined2 *)(iVar13 + 0xe58) = uVar11;
    *(undefined2 *)(iVar13 + 0xe5a) = uVar11;
    return;
  }
  uVar24 = iVar13 + 0x50;
  auVar26 = vectorSubtractFloatingPoint
                      (*(undefined1 (*) [16])(iVar12 + 0x810U & 0xfffffff0),
                       *(undefined1 (*) [16])(uVar24 & 0xfffffff0));
  local_88 = auVar26._8_4_;
  local_90 = auVar26._0_4_;
  iVar14 = *(int *)(iVar12 + 0x824);
  dVar25 = (double)SQRT(ABS(local_90 * local_90 + local_88 * local_88));
  if (iVar14 == 0x2c8) {
LAB_00835560:
    uVar11 = *(undefined2 *)(iVar12 + 0x820);
    *(undefined2 *)(iVar13 + 0xe58) = uVar11;
    *(undefined4 *)(iVar13 + 0xff4) = 3;
    *(undefined2 *)(iVar13 + 0xe5a) = uVar11;
  }
  else if (iVar14 < 0x2c9) {
    if (iVar14 == 0x13) {
      fVar1 = *(float *)(puVar20 + -0x7ff4);
      *(undefined2 *)(param_1 + 0xce) = 0xff;
      fVar2 = ABS(local_90);
      if (local_88 < fVar1) {
        fVar17 = fVar2 - local_88;
        fVar1 = *(float *)(puVar20 + -0x7fec);
        fVar16 = 1.0 / fVar17;
        local_88 = local_88 + fVar2;
        fVar2 = *(float *)(puVar20 + -0x7e68);
        fVar3 = *(float *)(puVar20 + -0x7e60);
        fVar4 = *(float *)(puVar20 + -0x7e80);
        fVar5 = *(float *)(puVar20 + -0x7e78);
        fVar6 = *(float *)(puVar20 + -0x7e58);
        fVar7 = *(float *)(puVar20 + -0x7e70);
        fVar8 = *(float *)(puVar20 + -0x7e50);
        fVar9 = *(float *)(puVar20 + -0x7e48);
        fVar18 = fVar17 * fVar16 - fVar1;
        fVar10 = *(float *)(puVar20 + -0x7e38);
      }
      else {
        fVar17 = local_88 + fVar2;
        fVar1 = *(float *)(puVar20 + -0x7fec);
        fVar16 = 1.0 / fVar17;
        local_88 = local_88 - fVar2;
        fVar2 = *(float *)(puVar20 + -0x7e68);
        fVar3 = *(float *)(puVar20 + -0x7e60);
        fVar4 = *(float *)(puVar20 + -0x7e80);
        fVar5 = *(float *)(puVar20 + -0x7e78);
        fVar6 = *(float *)(puVar20 + -0x7e58);
        fVar7 = *(float *)(puVar20 + -0x7e70);
        fVar8 = *(float *)(puVar20 + -0x7e50);
        fVar9 = *(float *)(puVar20 + -0x7e48);
        fVar18 = fVar17 * fVar16 - fVar1;
        fVar10 = *(float *)(puVar20 + -0x7e40);
      }
      fVar16 = fVar16 * -fVar18 + fVar16;
      local_88 = local_88 * (fVar16 * -(fVar17 * fVar16 - fVar1) + fVar16);
      fVar1 = local_88 * local_88;
      fVar10 = fVar10 - (local_88 * fVar1 * fVar1 * fVar1 * fVar1 *
                         (fVar1 * (fVar1 * (fVar1 * (fVar1 * fVar2 - fVar3) + fVar6) - fVar8) +
                         fVar9) + local_88 * fVar1 * (fVar1 * (fVar1 * fVar4 + fVar5) - fVar7) +
                                  local_88);
      if (local_90 < 0.0) {
        fVar10 = -fVar10;
      }
      uStack_6e = (undefined2)(int)(fVar10 * *(float *)(puVar20 + -0x7e30));
      *(undefined2 *)(param_1 + 0xcc) = uStack_6e;
      uVar11 = *(undefined2 *)(iVar12 + 0x820);
      *(undefined2 *)(iVar13 + 0xe58) = uVar11;
      *(undefined2 *)(iVar13 + 0xe5a) = uVar11;
      return;
    }
    if (iVar14 == 0x18) goto LAB_008357d8;
  }
  else {
    if (iVar14 == 0x2cc) {
LAB_008357d8:
      fVar1 = *(float *)(puVar20 + -0x7ff4);
      *(undefined2 *)(param_1 + 0xce) = 0xff;
      fVar2 = ABS(local_90);
      if (local_88 < fVar1) {
        fVar17 = fVar2 - local_88;
        fVar1 = *(float *)(puVar20 + -0x7fec);
        fVar16 = 1.0 / fVar17;
        local_88 = local_88 + fVar2;
        fVar2 = *(float *)(puVar20 + -0x7e68);
        fVar3 = *(float *)(puVar20 + -0x7e60);
        fVar4 = *(float *)(puVar20 + -0x7e80);
        fVar5 = *(float *)(puVar20 + -0x7e78);
        fVar6 = *(float *)(puVar20 + -0x7e58);
        fVar7 = *(float *)(puVar20 + -0x7e70);
        fVar8 = *(float *)(puVar20 + -0x7e50);
        fVar9 = *(float *)(puVar20 + -0x7e48);
        fVar18 = fVar17 * fVar16 - fVar1;
        fVar10 = *(float *)(puVar20 + -0x7e38);
      }
      else {
        fVar17 = local_88 + fVar2;
        fVar1 = *(float *)(puVar20 + -0x7fec);
        fVar16 = 1.0 / fVar17;
        local_88 = local_88 - fVar2;
        fVar2 = *(float *)(puVar20 + -0x7e68);
        fVar3 = *(float *)(puVar20 + -0x7e60);
        fVar4 = *(float *)(puVar20 + -0x7e80);
        fVar5 = *(float *)(puVar20 + -0x7e78);
        fVar6 = *(float *)(puVar20 + -0x7e58);
        fVar7 = *(float *)(puVar20 + -0x7e70);
        fVar8 = *(float *)(puVar20 + -0x7e50);
        fVar9 = *(float *)(puVar20 + -0x7e48);
        fVar18 = fVar17 * fVar16 - fVar1;
        fVar10 = *(float *)(puVar20 + -0x7e40);
      }
      fVar16 = fVar16 * -fVar18 + fVar16;
      local_88 = local_88 * (fVar16 * -(fVar17 * fVar16 - fVar1) + fVar16);
      fVar1 = local_88 * local_88;
      fVar10 = fVar10 - (local_88 * fVar1 * fVar1 * fVar1 * fVar1 *
                         (fVar1 * (fVar1 * (fVar1 * (fVar1 * fVar2 - fVar3) + fVar6) - fVar8) +
                         fVar9) + local_88 * fVar1 * (fVar1 * (fVar1 * fVar4 + fVar5) - fVar7) +
                                  local_88);
      if (local_90 < 0.0) {
        fVar10 = -fVar10;
      }
      uStack_6e = (undefined2)(int)(fVar10 * *(float *)(puVar20 + -0x7e30));
      *(undefined2 *)(param_1 + 0xcc) = uStack_6e;
      uVar11 = *(undefined2 *)(iVar12 + 0x820);
      *(undefined2 *)(iVar13 + 0xe58) = uVar11;
      *(undefined2 *)(iVar13 + 0xe5a) = uVar11;
      sVar22 = *(short *)(param_1 + 0xcc) - *(short *)(iVar12 + 0x820);
      if ((ushort)(sVar22 + 0x1fffU) < 0x4000) {
        *(undefined4 *)(iVar13 + 0xff4) = 1;
      }
      else if ((ushort)(sVar22 + 0xdfffU) < 0x4000) {
        *(undefined4 *)(iVar13 + 0xff4) = 2;
      }
      else if ((ushort)(sVar22 + 0x5fffU) < 0x4000) {
        *(undefined4 *)(iVar13 + 0xff4) = 4;
      }
      else {
        *(undefined4 *)(iVar13 + 0xff4) = 3;
      }
      return;
    }
    if (iVar14 == 0x2dd) goto LAB_00835560;
  }
  iVar14 = *(int *)(param_1 + 0x1c);
  iVar15 = *(int *)(iVar14 + 0x18);
  if (ABS(*(float *)(iVar14 + 0x814) - *(float *)(iVar15 + 0x54)) < *(float *)(puVar20 + -0x7fd4)) {
    _opd_FUN_0019a130(0x3f);
    _opd_FUN_001886a0(&DAT_00425000);
    _opd_FUN_001886d0(0);
    iVar21 = _opd_FUN_000a15b0((double)*(float *)(puVar20 + -0x7ff4),0x803f,0x10,auStack_80,
                               local_100);
    if (iVar21 != 0) goto LAB_0083559c;
    *(undefined4 *)(param_1 + 600) = 0;
  }
  else {
LAB_0083559c:
    iVar21 = *(int *)(param_1 + 600) + *(int *)(iVar14 + 0xa4);
    *(int *)(param_1 + 600) = iVar21;
    if (299 < iVar21) {
      _opd_FUN_003de4f0(iVar14);
      uVar11 = *(undefined2 *)(iVar14 + 0x820);
      *(undefined2 *)(iVar15 + 0xe5c) = uVar11;
      *(undefined2 *)(iVar15 + 0xe5a) = uVar11;
      *(undefined2 *)(iVar15 + 0xe58) = uVar11;
      *(undefined2 *)(iVar15 + 0xe5e) = uVar11;
      *(undefined4 *)(param_1 + 600) = 0;
      return;
    }
  }
  if ((double)*(float *)(puVar20 + -0x7e28) < dVar25) {
    _opd_FUN_003de4f0(iVar12);
    *(undefined1 *)(iVar12 + 0x831) = *(undefined1 *)(iVar12 + 0x830);
    return;
  }
  if ((double)*(float *)(puVar20 + -0x7e10) < dVar25) {
    fVar1 = *(float *)(puVar20 + -0x7ff4);
    fVar2 = ABS(local_90);
    *(undefined2 *)(param_1 + 0xce) = 0xff;
    if (local_88 < fVar1) {
      fVar16 = fVar2 - local_88;
      fVar1 = *(float *)(puVar20 + -0x7fec);
      fVar17 = 1.0 / fVar16;
      local_88 = local_88 + fVar2;
      fVar2 = *(float *)(puVar20 + -0x7e68);
      fVar3 = *(float *)(puVar20 + -0x7e60);
      fVar4 = *(float *)(puVar20 + -0x7e80);
      fVar5 = *(float *)(puVar20 + -0x7e78);
      fVar6 = *(float *)(puVar20 + -0x7e58);
      fVar7 = *(float *)(puVar20 + -0x7e70);
      fVar8 = *(float *)(puVar20 + -0x7e50);
      fVar9 = *(float *)(puVar20 + -0x7e48);
      fVar18 = fVar16 * fVar17 - fVar1;
      fVar10 = *(float *)(puVar20 + -0x7e38);
    }
    else {
      fVar16 = local_88 + fVar2;
      fVar1 = *(float *)(puVar20 + -0x7fec);
      fVar17 = 1.0 / fVar16;
      local_88 = local_88 - fVar2;
      fVar2 = *(float *)(puVar20 + -0x7e68);
      fVar3 = *(float *)(puVar20 + -0x7e60);
      fVar4 = *(float *)(puVar20 + -0x7e80);
      fVar5 = *(float *)(puVar20 + -0x7e78);
      fVar6 = *(float *)(puVar20 + -0x7e58);
      fVar7 = *(float *)(puVar20 + -0x7e70);
      fVar8 = *(float *)(puVar20 + -0x7e50);
      fVar9 = *(float *)(puVar20 + -0x7e48);
      fVar18 = fVar16 * fVar17 - fVar1;
      fVar10 = *(float *)(puVar20 + -0x7e40);
    }
    fVar17 = fVar17 * -fVar18 + fVar17;
    local_88 = local_88 * (fVar17 * -(fVar16 * fVar17 - fVar1) + fVar17);
    fVar1 = local_88 * local_88;
    fVar10 = fVar10 - (local_88 * fVar1 * fVar1 * fVar1 * fVar1 *
                       (fVar1 * (fVar1 * (fVar1 * (fVar1 * fVar2 - fVar3) + fVar6) - fVar8) + fVar9)
                      + local_88 * fVar1 * (fVar1 * (fVar1 * fVar4 + fVar5) - fVar7) + local_88);
    if (local_90 < 0.0) {
      fVar10 = -fVar10;
    }
    uStack_6e = (undefined2)(int)(fVar10 * *(float *)(puVar20 + -0x7e30));
    *(undefined2 *)(param_1 + 0xcc) = uStack_6e;
    if (*(uint *)(iVar12 + 0x828) < 0x44) {
                    /* WARNING: Could not recover jumptable at 0x008356b4. Too many branches */
                    /* WARNING: Treating indirect jump as call */
      (*(code *)(*(int *)(*(uint *)(iVar12 + 0x828) * 4 + *(int *)(puVar20 + -0x7e20)) +
                *(int *)(puVar20 + -0x7e20)))();
      return;
    }
    goto LAB_00835cf0;
  }
  if (dVar25 <= (double)*(float *)(puVar20 + -0x7e08)) {
    iVar14 = *(int *)(iVar12 + 0x828);
    if (iVar14 < 0x1d) {
      if (iVar14 < 0x1b) {
        if (0xd < iVar14) {
          if (0x10 < iVar14) {
            if ((iVar14 < 0x13) && (*(int *)(iVar12 + 0x82c) != 0)) {
              uVar11 = *(undefined2 *)(iVar12 + 0x820);
              *(undefined2 *)(iVar13 + 0xe5c) = uVar11;
              *(undefined2 *)(iVar13 + 0xe5a) = uVar11;
              *(undefined2 *)(iVar13 + 0xe58) = uVar11;
              *(undefined2 *)(iVar13 + 0xe5e) = uVar11;
            }
            goto LAB_00835cf0;
          }
          goto LAB_00835c1c;
        }
        if (7 < iVar14) {
LAB_00836118:
          if (*(char *)(iVar12 + 0x830) == '\0') {
            *(undefined2 *)(param_1 + 0xce) = 0x96;
          }
          else {
            *(undefined2 *)(param_1 + 0xce) = 0xff;
          }
          *(undefined2 *)(param_1 + 0xcc) = *(undefined2 *)(iVar12 + 0x820);
          goto LAB_00835cf0;
        }
        if (3 < iVar14 - 1U) goto LAB_00835cf0;
      }
    }
    else if (iVar14 < 0x32) {
      if (iVar14 < 0x2e) {
        if (iVar14 < 0x25) goto LAB_00835cf0;
        if (iVar14 < 0x27) goto LAB_00836118;
        if (iVar14 != 0x27) goto LAB_00835cf0;
LAB_00835c1c:
        if (*(char *)(iVar12 + 0x830) == '\0') {
          *(undefined2 *)(param_1 + 0xce) = 0x96;
        }
        else {
          *(undefined2 *)(param_1 + 0xce) = 0xff;
        }
        if (local_88 < *(float *)(puVar20 + -0x7ff4)) goto LAB_00836378;
        fVar16 = local_88 + ABS(local_90);
        fVar6 = *(float *)(puVar20 + -0x7fec);
        fVar17 = 1.0 / fVar16;
        local_88 = local_88 - ABS(local_90);
        fVar7 = *(float *)(puVar20 + -0x7e68);
        fVar8 = *(float *)(puVar20 + -0x7e60);
        fVar9 = *(float *)(puVar20 + -0x7e80);
        fVar10 = *(float *)(puVar20 + -0x7e78);
        fVar1 = *(float *)(puVar20 + -0x7e58);
        fVar2 = *(float *)(puVar20 + -0x7e70);
        fVar3 = *(float *)(puVar20 + -0x7e50);
        fVar4 = *(float *)(puVar20 + -0x7e48);
        fVar18 = fVar16 * fVar17 - fVar6;
        fVar5 = *(float *)(puVar20 + -0x7e40);
        goto LAB_00835c80;
      }
    }
    else if (iVar14 != 0x43) {
      if (0x43 < iVar14) {
        if ((iVar14 != 0x2c8) && (iVar14 != 0x2dd)) goto LAB_00835cf0;
        goto LAB_00835c1c;
      }
      if (1 < iVar14 - 0x3fU) goto LAB_00835cf0;
    }
    *(undefined4 *)(iVar12 + 0x814) = *(undefined4 *)(iVar13 + 0x54);
    auVar26 = *(undefined1 (*) [16])(uVar24 & 0xfffffff0);
    _opd_FUN_00171160(auStack_80,iVar12 + 0x810);
    auVar26 = vectorSubtractFloatingPoint(auVar26,*(undefined1 (*) [16])(uVar24 & 0xfffffff0));
    fStack_dc = auVar26._4_4_;
    local_e0 = auVar26._0_4_;
    local_d8 = auVar26._8_4_;
    if (*(float *)(puVar20 + -0x7e00) <=
        local_d8 * local_d8 + local_e0 * local_e0 + fStack_dc * fStack_dc) {
      uVar23 = *(ulonglong *)(iVar13 + 0xe48);
      fStack_e4 = auVar26._12_4_;
      pfVar19 = (float *)(iVar13 + 0xdf0U & 0xfffffff0);
      *pfVar19 = local_e0;
      pfVar19[1] = fStack_dc;
      pfVar19[2] = local_d8;
      pfVar19[3] = fStack_e4;
      *(ulonglong *)(iVar13 + 0xe48) = uVar23 | 0x400000000000000;
    }
    if ((*(int *)(iVar12 + 0x8c) == 2) && (*(int *)(iVar12 + 0x82c) == 0x29)) {
      *(undefined2 *)(iVar13 + 0xe5a) = *(undefined2 *)(iVar12 + 0x820);
    }
    else if (*(int *)(iVar12 + 0x90) == *(int *)(iVar12 + 0x828)) {
      uVar11 = *(undefined2 *)(iVar12 + 0x820);
      *(undefined2 *)(iVar13 + 0xe5e) = uVar11;
      *(undefined2 *)(iVar13 + 0xe5a) = uVar11;
    }
  }
  else {
    iVar13 = *(int *)(iVar12 + 0x828);
    if (iVar13 < 0x28) {
      if (0x24 < iVar13) {
LAB_00836180:
        if (*(char *)(iVar12 + 0x830) == '\0') {
          *(undefined2 *)(param_1 + 0xce) = 0x96;
        }
        else {
          *(undefined2 *)(param_1 + 0xce) = 0xff;
        }
        if (local_88 < *(float *)(puVar20 + -0x7ff4)) {
LAB_00836378:
          fVar16 = ABS(local_90) - local_88;
          fVar6 = *(float *)(puVar20 + -0x7fec);
          fVar17 = 1.0 / fVar16;
          local_88 = local_88 + ABS(local_90);
          fVar7 = *(float *)(puVar20 + -0x7e68);
          fVar8 = *(float *)(puVar20 + -0x7e60);
          fVar9 = *(float *)(puVar20 + -0x7e80);
          fVar10 = *(float *)(puVar20 + -0x7e78);
          fVar1 = *(float *)(puVar20 + -0x7e58);
          fVar2 = *(float *)(puVar20 + -0x7e70);
          fVar3 = *(float *)(puVar20 + -0x7e50);
          fVar4 = *(float *)(puVar20 + -0x7e48);
          fVar18 = fVar16 * fVar17 - fVar6;
          fVar5 = *(float *)(puVar20 + -0x7e38);
LAB_00835c80:
          fVar17 = fVar17 * -fVar18 + fVar17;
          local_88 = local_88 * (fVar17 * -(fVar16 * fVar17 - fVar6) + fVar17);
          fVar6 = local_88 * local_88;
          fVar8 = fVar6 * fVar7 - fVar8;
          fVar10 = fVar6 * fVar9 + fVar10;
        }
        else {
          fVar6 = local_88 + ABS(local_90);
          fVar7 = 1.0 / fVar6;
          fVar1 = *(float *)(puVar20 + -0x7e58);
          fVar2 = *(float *)(puVar20 + -0x7e70);
          fVar3 = *(float *)(puVar20 + -0x7e50);
          fVar4 = *(float *)(puVar20 + -0x7e48);
          fVar5 = *(float *)(puVar20 + -0x7e40);
          fVar7 = fVar7 * -(fVar6 * fVar7 - *(float *)(puVar20 + -0x7fec)) + fVar7;
          local_88 = (local_88 - ABS(local_90)) *
                     (fVar7 * -(fVar6 * fVar7 - *(float *)(puVar20 + -0x7fec)) + fVar7);
          fVar6 = local_88 * local_88;
          fVar8 = fVar6 * *(float *)(puVar20 + -0x7e68) - *(float *)(puVar20 + -0x7e60);
          fVar10 = fVar6 * *(float *)(puVar20 + -0x7e80) + *(float *)(puVar20 + -0x7e78);
        }
        fVar5 = fVar5 - (local_88 * fVar6 * fVar6 * fVar6 * fVar6 *
                         (fVar6 * (fVar6 * (fVar6 * fVar8 + fVar1) - fVar3) + fVar4) +
                        local_88 * fVar6 * (fVar6 * fVar10 - fVar2) + local_88);
        if (local_90 < 0.0) {
          fVar5 = -fVar5;
        }
        uStack_6e = (undefined2)(int)(fVar5 * *(float *)(puVar20 + -0x7e30));
        *(undefined2 *)(param_1 + 0xcc) = uStack_6e;
        goto LAB_00835cf0;
      }
      if (iVar13 < 0x11) {
        if (7 < iVar13) goto LAB_00836180;
        if (3 < iVar13 - 1U) goto LAB_00835cf0;
      }
      else if ((0x12 < iVar13) && (iVar13 != 0x1b)) goto LAB_00835cf0;
    }
    else if (iVar13 < 0x41) {
      if ((iVar13 < 0x3f) && (1 < iVar13 - 0x2eU)) goto LAB_00835cf0;
    }
    else {
      if ((iVar13 == 0x2c8) || (iVar13 == 0x2dd)) goto LAB_00836180;
      if (iVar13 != 0x43) goto LAB_00835cf0;
    }
    *(char *)(iVar12 + 0x830) = *(char *)(iVar12 + 0x831);
    if (*(char *)(iVar12 + 0x831) == '\0') {
      *(undefined2 *)(param_1 + 0xce) = 0x96;
    }
    else {
      *(undefined2 *)(param_1 + 0xce) = 0xff;
    }
    fVar1 = ABS(local_90);
    if (local_88 < *(float *)(puVar20 + -0x7ff4)) {
      fVar7 = fVar1 - local_88;
      fVar8 = 1.0 / fVar7;
      fVar4 = *(float *)(puVar20 + -0x7e58);
      fVar3 = *(float *)(puVar20 + -0x7e70);
      fVar2 = *(float *)(puVar20 + -0x7e50);
      fVar6 = *(float *)(puVar20 + -0x7e48);
      fVar5 = *(float *)(puVar20 + -0x7e38);
      fVar8 = fVar8 * -(fVar7 * fVar8 - *(float *)(puVar20 + -0x7fec)) + fVar8;
      fVar1 = (local_88 + fVar1) *
              (fVar8 * -(fVar7 * fVar8 - *(float *)(puVar20 + -0x7fec)) + fVar8);
      fVar8 = fVar1 * fVar1;
      fVar7 = fVar8 * *(float *)(puVar20 + -0x7e68) - *(float *)(puVar20 + -0x7e60);
      fVar9 = fVar8 * *(float *)(puVar20 + -0x7e80) + *(float *)(puVar20 + -0x7e78);
    }
    else {
      fVar7 = local_88 + fVar1;
      fVar8 = 1.0 / fVar7;
      fVar4 = *(float *)(puVar20 + -0x7e58);
      fVar3 = *(float *)(puVar20 + -0x7e70);
      fVar2 = *(float *)(puVar20 + -0x7e50);
      fVar6 = *(float *)(puVar20 + -0x7e48);
      fVar5 = *(float *)(puVar20 + -0x7e40);
      fVar8 = fVar8 * -(fVar7 * fVar8 - *(float *)(puVar20 + -0x7fec)) + fVar8;
      fVar1 = (local_88 - fVar1) *
              (fVar8 * -(fVar7 * fVar8 - *(float *)(puVar20 + -0x7fec)) + fVar8);
      fVar8 = fVar1 * fVar1;
      fVar7 = fVar8 * *(float *)(puVar20 + -0x7e68) - *(float *)(puVar20 + -0x7e60);
      fVar9 = fVar8 * *(float *)(puVar20 + -0x7e80) + *(float *)(puVar20 + -0x7e78);
    }
    fVar5 = fVar5 - (fVar1 * fVar8 * fVar8 * fVar8 * fVar8 *
                     (fVar8 * (fVar8 * (fVar8 * fVar7 + fVar4) - fVar2) + fVar6) +
                    fVar1 * fVar8 * (fVar8 * fVar9 - fVar3) + fVar1);
    if (local_90 < 0.0) {
      fVar5 = -fVar5;
    }
    uStack_6e = (undefined2)(int)(fVar5 * *(float *)(puVar20 + -0x7e30));
    *(undefined2 *)(param_1 + 0xcc) = uStack_6e;
    if (*(uint *)(iVar12 + 0x828) < 0x44) {
                    /* WARNING: Could not recover jumptable at 0x00835a04. Too many branches */
                    /* WARNING: Treating indirect jump as call */
      (*(code *)(*(int *)(*(uint *)(iVar12 + 0x828) * 4 + *(int *)(puVar20 + -0x7e1c)) +
                *(int *)(puVar20 + -0x7e1c)))();
      return;
    }
  }
LAB_00835cf0:
  *(undefined1 *)(iVar12 + 0x831) = *(undefined1 *)(iVar12 + 0x830);
  return;
}

