// hook SITE 0x005d32d4

longlong _opd_FUN_005d31e8(int param_1,int param_2)

{
  uint uVar1;
  undefined4 *puVar2;
  undefined *puVar3;
  float fVar4;
  int iVar6;
  longlong lVar5;
  int iVar7;
  int iVar8;
  uint uVar9;
  undefined1 *puVar10;
  undefined1 *puVar11;
  float *pfVar13;
  undefined8 uVar12;
  longlong lVar14;
  float *pfVar15;
  undefined *puVar16;
  undefined4 uVar17;
  undefined4 uVar18;
  undefined4 uVar19;
  undefined4 uVar20;
  undefined4 uVar21;
  undefined4 uVar22;
  undefined4 uVar23;
  undefined4 uVar24;
  undefined4 uVar25;
  undefined4 uVar26;
  undefined4 uVar27;
  undefined4 uVar28;
  undefined4 uVar29;
  undefined4 uVar30;
  undefined4 uVar31;
  undefined4 uVar32;
  float local_120;
  float fStack_11c;
  float local_118;
  float local_110;
  float fStack_10c;
  float local_108;
  undefined1 local_100 [16];
  undefined1 local_f0 [16];
  float local_e0;
  float fStack_dc;
  float local_d8;
  float local_d0;
  float fStack_cc;
  float local_c8;
  undefined1 local_c0 [16];
  undefined1 local_b0 [88];
  
  puVar3 = PTR_PTR_0121b414;
  iVar6 = _opd_FUN_000be778(0x2a0,0xa00000000000000);
  if (iVar6 == 0) {
    return 0;
  }
  lVar5 = _opd_FUN_000bead8(iVar6,0xa00000000000000,*(undefined4 *)(puVar3 + -0x7fd0),
                            *(undefined4 *)(puVar3 + -0x7fcc));
  if (lVar5 == 0) goto LAB_005d36a0;
  *(int *)(iVar6 + 0x294) = param_1;
  *(undefined4 *)(iVar6 + 0x284) = 0;
  if (param_1 == 0) {
    iVar7 = _opd_FUN_00087a78();
    iVar8 = -1;
    if (iVar7 != 0) {
      iVar7 = _opd_FUN_00087a78();
      iVar8 = (int)*(short *)(iVar7 + 0xc);
    }
    *(int *)(iVar6 + 0x288) = iVar8;
    if (*(int *)(iVar6 + 0x284) != 0) goto LAB_005d32a0;
    if (iVar8 < 1) goto LAB_005d36a0;
LAB_005d36f0:
    puVar16 = (undefined *)0x883781;
  }
  else {
    *(undefined4 *)(iVar6 + 0x288) = 10;
LAB_005d32a0:
    if (*(int *)(iVar6 + 0x284) == 0) goto LAB_005d36f0;
    puVar16 = &DAT_003d0cf8;
    if (*(int *)(iVar6 + 0x284) != 1) {
      puVar16 = (undefined *)0x0;
    }
  }
  iVar7 = _opd_FUN_005e68c8(puVar16,0xd,0);
  if (iVar7 == 0) {
LAB_005d36a0:
    _opd_FUN_000c1e00(iVar6);
    return 0;
  }
  iVar7 = _opd_FUN_00117da0(iVar7,0);
  *(int *)(iVar6 + 0x60) = iVar7;
  if (iVar7 == 0) goto LAB_005d36a0;
  _opd_FUN_00113030(iVar7,0);
  iVar8 = _opd_FUN_00ccf650(iVar7,0);
  *(int *)(iVar6 + 0x220) = iVar8;
  if (iVar8 != 0) {
    _opd_FUN_00ccf438(*(undefined4 *)(iVar6 + 0x60),iVar8);
  }
  uVar1 = *(uint *)(puVar3 + -0x7fc0);
  uVar9 = *(uint *)(puVar3 + -0x7fbc);
  puVar2 = (undefined4 *)(*(uint *)(puVar3 + -0x7fc8) & 0xfffffff0);
  uVar17 = *puVar2;
  uVar18 = puVar2[1];
  uVar19 = puVar2[2];
  uVar20 = puVar2[3];
  puVar2 = (undefined4 *)(*(uint *)(puVar3 + -0x7fc4) & 0xfffffff0);
  uVar21 = *puVar2;
  uVar22 = puVar2[1];
  uVar23 = puVar2[2];
  uVar24 = puVar2[3];
  *(int *)(iVar7 + 0x54) = iVar6 + 0x70;
  puVar2 = (undefined4 *)(uVar1 & 0xfffffff0);
  uVar29 = *puVar2;
  uVar30 = puVar2[1];
  uVar31 = puVar2[2];
  uVar32 = puVar2[3];
  puVar2 = (undefined4 *)(uVar9 & 0xfffffff0);
  uVar25 = *puVar2;
  uVar26 = puVar2[1];
  uVar27 = puVar2[2];
  uVar28 = puVar2[3];
  puVar2 = (undefined4 *)(iVar6 + 0x240U & 0xfffffff0);
  *puVar2 = uVar17;
  puVar2[1] = uVar18;
  puVar2[2] = uVar19;
  puVar2[3] = uVar20;
  puVar2 = (undefined4 *)(iVar6 + 0x250U & 0xfffffff0);
  *puVar2 = uVar21;
  puVar2[1] = uVar22;
  puVar2[2] = uVar23;
  puVar2[3] = uVar24;
  puVar2 = (undefined4 *)(iVar6 + 0x260U & 0xfffffff0);
  *puVar2 = uVar29;
  puVar2[1] = uVar30;
  puVar2[2] = uVar31;
  puVar2[3] = uVar32;
  puVar2 = (undefined4 *)(iVar6 + 0x270U & 0xfffffff0);
  *puVar2 = uVar25;
  puVar2[1] = uVar26;
  puVar2[2] = uVar27;
  puVar2[3] = uVar28;
  if (*(int *)(iVar6 + 0x284) == 0) {
    iVar8 = *(int *)(iVar7 + 0xcc);
    fStack_cc = (float)*(undefined8 *)(iVar8 + 0x150);
    local_d0 = (float)((ulonglong)*(undefined8 *)(iVar8 + 0x150) >> 0x20);
    local_c8 = (float)((ulonglong)*(undefined8 *)(iVar8 + 0x158) >> 0x20);
    fStack_11c = (float)*(undefined8 *)(iVar8 + 0x140);
    local_120 = (float)((ulonglong)*(undefined8 *)(iVar8 + 0x140) >> 0x20);
    local_118 = (float)((ulonglong)*(undefined8 *)(iVar8 + 0x148) >> 0x20);
    if (1 < *(uint *)(iVar7 + 0xa8)) {
      lVar14 = (ulonglong)~(1 - *(uint *)(iVar7 + 0xa8)) + 1;
      pfVar15 = (float *)(iVar8 + 0x2c0);
      pfVar13 = (float *)(iVar8 + 0x2b0);
      do {
        fVar4 = pfVar15[2];
        if (local_c8 - pfVar15[2] < 0.0) {
          fVar4 = local_c8;
        }
        local_c8 = fVar4;
        fVar4 = *pfVar15;
        if (local_d0 - *pfVar15 < 0.0) {
          fVar4 = local_d0;
        }
        local_d0 = fVar4;
        fVar4 = pfVar15[1];
        if (fStack_cc - pfVar15[1] < 0.0) {
          fVar4 = fStack_cc;
        }
        fStack_cc = fVar4;
        if (local_118 - pfVar13[2] < 0.0) {
          local_118 = pfVar13[2];
        }
        if (local_120 - *pfVar13 < 0.0) {
          local_120 = *pfVar13;
        }
        if (fStack_11c - pfVar13[1] < 0.0) {
          fStack_11c = pfVar13[1];
        }
        lVar14 = lVar14 + -1;
        pfVar15 = pfVar15 + 0x5c;
        pfVar13 = pfVar13 + 0x5c;
      } while (lVar14 != 0);
    }
    if (*(int *)(iVar6 + 0x284) == 0) {
      uVar17 = *(undefined4 *)(puVar3 + -0x7fb8);
      puVar10 = local_c0;
      puVar11 = local_b0;
      goto LAB_005d38cc;
    }
    if (*(int *)(iVar6 + 0x284) == 1) {
      puVar10 = local_c0;
      puVar11 = local_b0;
      uVar12 = 0xf;
    }
    else {
      puVar10 = local_c0;
      puVar11 = local_b0;
      uVar12 = 1;
    }
  }
  else {
    iVar8 = *(int *)(iVar7 + 0xcc);
    fStack_10c = (float)*(undefined8 *)(iVar8 + 0x150);
    local_110 = (float)((ulonglong)*(undefined8 *)(iVar8 + 0x150) >> 0x20);
    local_108 = (float)((ulonglong)*(undefined8 *)(iVar8 + 0x158) >> 0x20);
    fStack_dc = (float)*(undefined8 *)(iVar8 + 0x140);
    local_e0 = (float)((ulonglong)*(undefined8 *)(iVar8 + 0x140) >> 0x20);
    local_d8 = (float)((ulonglong)*(undefined8 *)(iVar8 + 0x148) >> 0x20);
    if (1 < *(uint *)(iVar7 + 0xa8)) {
      lVar14 = (ulonglong)~(1 - *(uint *)(iVar7 + 0xa8)) + 1;
      pfVar15 = (float *)(iVar8 + 0x2c0);
      pfVar13 = (float *)(iVar8 + 0x2b0);
      do {
        fVar4 = pfVar15[2];
        if (local_108 - pfVar15[2] < 0.0) {
          fVar4 = local_108;
        }
        local_108 = fVar4;
        fVar4 = *pfVar15;
        if (local_110 - *pfVar15 < 0.0) {
          fVar4 = local_110;
        }
        local_110 = fVar4;
        fVar4 = pfVar15[1];
        if (fStack_10c - pfVar15[1] < 0.0) {
          fVar4 = fStack_10c;
        }
        fStack_10c = fVar4;
        if (local_d8 - pfVar13[2] < 0.0) {
          local_d8 = pfVar13[2];
        }
        if (local_e0 - *pfVar13 < 0.0) {
          local_e0 = *pfVar13;
        }
        if (fStack_dc - pfVar13[1] < 0.0) {
          fStack_dc = pfVar13[1];
        }
        lVar14 = lVar14 + -1;
        pfVar15 = pfVar15 + 0x5c;
        pfVar13 = pfVar13 + 0x5c;
      } while (lVar14 != 0);
    }
    if (*(int *)(iVar6 + 0x284) == 0) {
      uVar17 = *(undefined4 *)(puVar3 + -0x7fb8);
      puVar10 = local_100;
      puVar11 = local_f0;
LAB_005d38cc:
      iVar7 = _opd_FUN_000639a8(0x40101f,iVar7 + 0x10,puVar10,puVar11,0,0x1e,0,uVar17);
      if (iVar7 != 0) {
        uVar1 = *(uint *)(iVar7 + 0x10);
        uVar9 = _opd_FUN_000cdc90(*(undefined4 *)(puVar3 + -0x7fb4));
        *(uint *)(iVar7 + 0x10) = uVar1 | uVar9 & 0xffffff;
      }
      goto LAB_005d3588;
    }
    if (*(int *)(iVar6 + 0x284) == 1) {
      puVar10 = local_100;
      puVar11 = local_f0;
      uVar12 = 0xf;
    }
    else {
      puVar10 = local_100;
      puVar11 = local_f0;
      uVar12 = 1;
    }
  }
  iVar7 = _opd_FUN_000639a8(0x401010,iVar7 + 0x10,puVar10,puVar11,0,uVar12,0,0);
  if (iVar7 != 0) {
    uVar1 = *(uint *)(iVar7 + 0x10);
    uVar9 = _opd_FUN_000cdc90(*(undefined4 *)(puVar3 + -0x7fb0));
    *(uint *)(iVar7 + 0x10) = uVar1 | uVar9 & 0xffffff;
  }
LAB_005d3588:
  _opd_FUN_0019b730(iVar7);
  *(int *)(iVar6 + 0x290) = iVar7;
  if ((*(int *)(iVar6 + 0x284) == 1) &&
     (lVar14 = _opd_FUN_00b034b0(*(undefined4 *)(iVar6 + 0x60),iVar6 + 0x240U), lVar14 != 0)) {
    _opd_FUN_000beea8(iVar6,lVar14);
  }
  *(undefined4 *)(iVar6 + 0x34) = *(undefined4 *)(puVar3 + -0x7fac);
  if (*(int *)(iVar6 + 0x294) == 0) {
    if (*(int *)(iVar6 + 0x284) == 0) {
      *(undefined8 *)(*(int *)(puVar3 + -0x8000) + 8) = *(undefined8 *)(iVar6 + 0x20);
    }
    else if (*(int *)(iVar6 + 0x284) == 1) {
      **(undefined8 **)(puVar3 + -0x8000) = *(undefined8 *)(iVar6 + 0x20);
    }
  }
  *(uint *)(iVar6 + 0x280) = *(uint *)(iVar6 + 0x280) | 8;
  *(uint *)(*(int *)(iVar6 + 0x60) + 0x5c) = *(uint *)(*(int *)(iVar6 + 0x60) + 0x5c) | 7;
  if (param_2 != 0) {
    lVar14 = _opd_FUN_00e29bf0((double)*(float *)(puVar3 + -0x7f98),
                               (double)*(float *)(puVar3 + -0x7fa8),*(undefined4 *)(iVar6 + 0x60),
                               puVar16);
    *(longlong *)(iVar6 + 0x298) = lVar14;
    if (lVar14 != 0) {
      _opd_FUN_000beea8(iVar6,lVar14);
    }
  }
  return lVar5;
}

