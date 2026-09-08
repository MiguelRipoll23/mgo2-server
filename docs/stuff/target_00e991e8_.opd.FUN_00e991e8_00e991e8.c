// original call TARGET 0x00e991e8

/* WARNING: Restarted to delay deadcode elimination for space: stack */

void _opd_FUN_00e991e8(int param_1,undefined4 *param_2)

{
  bool bVar1;
  undefined4 *puVar2;
  undefined1 auVar3 [16];
  undefined1 auVar4 [16];
  undefined1 auVar5 [16];
  undefined *puVar6;
  int iVar7;
  int iVar8;
  byte bVar9;
  undefined4 uVar11;
  char cVar14;
  undefined4 uVar12;
  int iVar13;
  undefined8 uVar10;
  uint uVar15;
  int iVar16;
  byte *pbVar17;
  int iVar18;
  longlong lVar19;
  int iVar20;
  longlong lVar21;
  ulonglong uVar22;
  uint uVar23;
  int iVar24;
  longlong lVar25;
  int iVar27;
  ulonglong uVar26;
  int *piVar28;
  float fVar29;
  int iVar30;
  int iVar32;
  ulonglong uVar31;
  undefined4 *puVar33;
  double dVar34;
  undefined4 uVar35;
  undefined4 uVar36;
  undefined4 uVar37;
  undefined4 uVar38;
  undefined4 uVar40;
  undefined4 uVar41;
  undefined1 auVar39 [16];
  undefined4 uVar42;
  undefined1 auVar43 [16];
  undefined1 auVar44 [16];
  undefined1 auVar45 [16];
  undefined1 auVar46 [16];
  undefined1 auVar47 [16];
  undefined1 auVar48 [16];
  undefined4 uVar49;
  undefined4 uVar51;
  undefined4 uVar52;
  undefined1 auVar50 [16];
  undefined4 uVar53;
  undefined4 uVar54;
  undefined4 uVar58;
  undefined4 uVar59;
  undefined1 auVar55 [16];
  undefined1 auVar56 [16];
  undefined1 auVar57 [16];
  undefined4 uVar60;
  undefined4 uVar61;
  undefined4 uVar62;
  undefined4 local_1b0;
  undefined4 uStack_1ac;
  undefined4 uStack_1a8;
  undefined4 uStack_1a4;
  undefined4 uStack_190;
  undefined4 uStack_18c;
  undefined4 uStack_188;
  undefined4 uStack_184;
  undefined4 uStack_180;
  undefined4 uStack_17c;
  undefined4 uStack_178;
  undefined4 uStack_174;
  undefined1 local_170;
  undefined1 uStack_16f;
  undefined1 uStack_16e;
  undefined1 uStack_16d;
  undefined1 local_16c;
  undefined1 uStack_16b;
  undefined1 uStack_16a;
  undefined1 uStack_169;
  undefined1 local_168;
  undefined1 uStack_167;
  undefined1 uStack_166;
  undefined1 uStack_165;
  undefined1 local_164;
  undefined1 uStack_163;
  undefined1 uStack_162;
  undefined1 uStack_161;
  int local_160 [4];
  undefined4 local_150;
  undefined4 local_14c;
  undefined4 local_148;
  undefined4 local_144;
  undefined4 local_140;
  undefined4 local_13c;
  undefined4 local_138 [11];
  undefined4 local_10c;
  undefined4 local_108;
  undefined4 local_104;
  undefined4 uStack_100;
  undefined4 uStack_fc;
  undefined4 uStack_f8;
  undefined4 uStack_f4;
  undefined4 uStack_f0;
  undefined4 uStack_ec;
  undefined4 uStack_e8;
  undefined4 uStack_e4;
  undefined1 auStack_e0 [16];
  ulonglong local_d0;
  longlong local_c8;
  ulonglong local_c0;
  longlong local_b0;
  float local_a8;
  
  puVar6 = PTR_PTR_0121c31c;
  uVar26 = ZEXT48(&stack0x00000000);
  local_138[0] = 0;
  local_160[0] = _opd_FUN_008d2a70(*param_2,*(undefined4 *)(param_1 + 4));
  local_138[1] = 1;
  local_b0 = uVar26 - 0x138;
  local_160[1] = _opd_FUN_008d2a70(param_2[1],*(undefined4 *)(param_1 + 4));
  local_138[2] = 2;
  local_160[2] = _opd_FUN_008d2a70(param_2[2],*(undefined4 *)(param_1 + 4));
  local_138[3] = 3;
  local_160[3] = _opd_FUN_008d2a70(param_2[3],*(undefined4 *)(param_1 + 4));
  local_138[4] = 4;
  local_150 = _opd_FUN_008d2a70(param_2[4],*(undefined4 *)(param_1 + 4));
  local_138[5] = 5;
  local_14c = _opd_FUN_008d2a70(param_2[5],*(undefined4 *)(param_1 + 4));
  local_138[6] = 6;
  local_148 = _opd_FUN_008d2a70(param_2[6],*(undefined4 *)(param_1 + 4));
  local_138[7] = 7;
  local_144 = _opd_FUN_008d2a70(param_2[7],*(undefined4 *)(param_1 + 4));
  local_138[8] = 8;
  local_140 = _opd_FUN_008d2a70(param_2[8],*(undefined4 *)(param_1 + 4));
  local_138[9] = 9;
  local_13c = _opd_FUN_008d2a70(param_2[9],*(undefined4 *)(param_1 + 4));
  uVar22 = 0;
  uVar31 = 1;
  do {
    iVar13 = (int)uVar31;
    iVar30 = (int)((uVar22 & 0xffffffff) << 2);
    lVar25 = (uVar31 & 0x3fffffff) * 4;
    lVar21 = (9 - uVar31 & 0xffffffff) + 1;
    lVar19 = lVar25 + local_b0;
    lVar25 = lVar25 + (uVar26 - 0x160);
    if (10 < iVar13 + 1) {
      lVar21 = 1;
    }
    do {
      piVar28 = (int *)lVar25;
      puVar33 = (undefined4 *)lVar19;
      lVar25 = lVar25 + 4;
      lVar19 = lVar19 + 4;
      iVar32 = *piVar28;
      if (iVar32 < *(int *)((int)local_160 + iVar30)) {
        uVar11 = *puVar33;
        uVar12 = *(undefined4 *)((int)local_138 + iVar30);
        *piVar28 = *(int *)((int)local_160 + iVar30);
        *puVar33 = uVar12;
        *(int *)((int)local_160 + iVar30) = iVar32;
        *(undefined4 *)((int)local_138 + iVar30) = uVar11;
      }
      lVar21 = lVar21 + -1;
    } while (lVar21 != 0);
    uVar22 = uVar31;
    uVar31 = uVar31 + 1;
  } while (iVar13 != 9);
  iVar30 = 0;
LAB_00e994e4:
  do {
    iVar13 = *(int *)(iVar30 + (int)local_b0);
    iVar32 = param_1 + iVar13 * 8 + 0x8b0;
    if (*(longlong *)(iVar32 + 8) != 0) {
      _opd_FUN_000c2438();
      *(undefined8 *)(iVar32 + 8) = 0;
    }
    iVar32 = iVar13 * 4;
    iVar7 = iVar32 + 0x480;
    if (*(int *)(param_1 + iVar7) != 0) {
      _opd_FUN_001126d0(*(int *)(param_1 + iVar7));
      _opd_FUN_00112890(*(undefined4 *)(param_1 + iVar7));
      *(undefined4 *)(param_1 + iVar7) = 0;
    }
    uVar11 = param_2[iVar13];
    *(undefined4 *)(param_1 + iVar32 + 0x430) = uVar11;
    if (param_2[5] == 0x8f) {
      if (iVar13 == 1) {
        uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7be0));
        *(undefined4 *)(param_1 + 0x45c) = uVar11;
      }
      else {
LAB_00e994b0:
        *(undefined4 *)(param_1 + iVar32 + 0x458) = 0;
      }
LAB_00e994c4:
      iVar7 = *(int *)(param_1 + iVar32 + 0x480);
    }
    else {
      if (param_2[1] == 0x13) {
        if ((((iVar13 == 0) || (iVar13 == 5)) || (iVar13 == 3)) || ((iVar13 == 8 || (iVar13 == 9))))
        {
          uVar11 = _opd_FUN_008d29f0(uVar11,*(undefined4 *)(param_1 + 4));
          *(undefined4 *)(param_1 + iVar32 + 0x458) = uVar11;
        }
        else {
          if (iVar13 != 1) goto LAB_00e994b0;
          uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7bdc));
          *(undefined4 *)(param_1 + 0x45c) = uVar11;
        }
        goto LAB_00e994c4;
      }
      uVar11 = _opd_FUN_008d29f0(uVar11,*(undefined4 *)(param_1 + 4));
      *(undefined4 *)(param_1 + iVar32 + 0x458) = uVar11;
      if ((iVar13 != 2) || ((iVar7 = param_2[4], 2 < iVar7 - 0x3bU && (iVar7 != 0x40))))
      goto LAB_00e994c4;
      if (*(int *)(param_1 + 4) == 0) {
        uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7bd8));
        *(undefined4 *)(param_1 + 0x460) = uVar11;
        goto LAB_00e994c4;
      }
      uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7bd4));
      *(undefined4 *)(param_1 + 0x460) = uVar11;
      iVar7 = *(int *)(param_1 + 0x488);
    }
    if (iVar7 != 0) {
LAB_00e994d8:
      bVar1 = iVar30 == 0x24;
      iVar30 = iVar30 + 4;
      if (bVar1) break;
      goto LAB_00e994e4;
    }
    uVar15 = *(uint *)(param_1 + iVar32 + 0x458);
    if (uVar15 == 0) goto LAB_00e994d8;
    iVar7 = _opd_FUN_000c5ab0(uVar15 | 0xd000000);
    if (iVar7 == 0) {
      return;
    }
    local_c0 = _opd_FUN_00117da0(iVar7,0);
    iVar7 = (int)local_c0;
    *(int *)(param_1 + iVar32 + 0x480) = iVar7;
    if (iVar7 == 0) {
      return;
    }
    local_c0 = local_c0 & 0xffffffff;
    _opd_FUN_00113030(local_c0,0);
    if (*(int *)(iVar7 + 0xac) != 0) {
      uVar15 = 0;
      iVar8 = *(int *)(param_1 + 0x10);
      iVar16 = 0;
      pbVar17 = (byte *)(param_1 + iVar13 * 100 + 0x4d0);
      do {
        uVar23 = *(uint *)(iVar8 + 0xac);
        piVar28 = *(int **)(iVar8 + 0xd0);
        iVar24 = *(int *)(iVar16 + *(int *)(iVar7 + 0xd0));
        if ((int)uVar23 < 1) {
LAB_00e996dc:
          bVar9 = 0;
        }
        else {
          iVar20 = 0;
          lVar19 = ((ulonglong)uVar23 - 1 & 0xffffffff) + 1;
          if (*piVar28 == iVar24) goto LAB_00e996dc;
          do {
            piVar28 = piVar28 + 0x14;
            iVar20 = iVar20 + 1;
            lVar19 = lVar19 + -1;
            if (lVar19 == 0) goto LAB_00e996dc;
          } while (iVar24 != *piVar28);
          lVar19 = ((longlong)iVar20 - 0xffU & (longlong)((longlong)iVar20 - 0xffU) >> 0x3f) + 0xff;
          bVar9 = (byte)lVar19 & ~(byte)(lVar19 >> 0x3f);
        }
        uVar15 = uVar15 + 1;
        iVar16 = iVar16 + 0x50;
        *pbVar17 = bVar9;
        pbVar17 = pbVar17 + 1;
      } while (uVar15 < *(uint *)(iVar7 + 0xac));
    }
    iVar7 = _opd_FUN_008d2770(*(undefined4 *)(param_1 + iVar32 + 0x430),*(undefined4 *)(param_1 + 4)
                             );
    if (iVar7 == 0) {
      *(undefined4 *)(param_1 + iVar32 + 0x908) = 0;
    }
    else {
      iVar8 = iVar32 + 0x480;
      uVar11 = *(undefined4 *)(param_1 + iVar8);
      uVar12 = _opd_FUN_00da28f8(iVar7);
      uVar10 = _opd_FUN_009075e8(param_1 + 0x10,uVar11,0,uVar12);
      iVar7 = *(int *)(param_1 + iVar8);
      *(undefined8 *)(param_1 + iVar13 * 8 + 0x8b8) = uVar10;
      *(undefined4 *)(param_1 + iVar32 + 0x908) = 1;
      iVar13 = *(int *)(iVar7 + 0xd0);
      if (*(int *)(iVar7 + 0xac) != 0) {
        auVar3 = *(undefined1 (*) [16])(*(uint *)(puVar6 + -0x7bd0) & 0xfffffff0);
        auVar45 = *(undefined1 (*) [16])(*(uint *)(puVar6 + -0x7bb8) & 0xfffffff0);
        auVar46 = *(undefined1 (*) [16])(*(uint *)(puVar6 + -0x7bb4) & 0xfffffff0);
        auVar4 = *(undefined1 (*) [16])(*(uint *)(puVar6 + -0x7bcc) & 0xfffffff0);
        auVar5 = *(undefined1 (*) [16])(*(uint *)(puVar6 + -0x7bc8) & 0xfffffff0);
        puVar33 = (undefined4 *)(*(uint *)(puVar6 + -0x7bc4) & 0xfffffff0);
        uVar11 = *puVar33;
        uVar12 = puVar33[1];
        uVar61 = puVar33[2];
        uVar62 = puVar33[3];
        auVar43 = *(undefined1 (*) [16])(*(uint *)(puVar6 + -0x7bc0) & 0xfffffff0);
        iVar32 = 0;
        local_d0 = uVar26 - 0x1a0 & 0xffffffff;
        auVar44 = *(undefined1 (*) [16])(*(uint *)(puVar6 + -0x7bbc) & 0xfffffff0);
        iVar16 = 0;
        do {
          iVar24 = *(int *)(param_1 + 0x10);
          piVar28 = *(int **)(iVar24 + 0xd0);
          iVar20 = *(int *)(iVar16 + *(int *)(iVar7 + 0xd0));
          if ((int)*(uint *)(iVar24 + 0xac) < 1) {
LAB_00e9a29c:
            iVar24 = iVar13 + iVar16;
            uVar15 = *(int *)(iVar24 + 8) * 0x40 + *(int *)(iVar7 + 0xd4);
            auStack_e0._4_4_ = uVar12;
            auStack_e0._0_4_ = uVar11;
            auStack_e0._8_4_ = uVar61;
            auStack_e0._12_4_ = uVar62;
            auVar50 = vectorPermute(auVar3,auVar3,auVar43);
            auVar48 = vectorPermute(auVar3,auVar3,auVar44);
            auVar47 = vectorPermute(auVar3,auVar3,auVar46);
            auVar39 = vectorMultiplyAddFloatingPoint
                                (*(undefined1 (*) [16])(uVar15 & 0xfffffff0),auVar50,
                                 (undefined1  [16])0x0);
            auVar50 = vectorPermute(auVar3,auVar3,auVar45);
            auVar48 = vectorMultiplyAddFloatingPoint
                                (*(undefined1 (*) [16])(uVar15 + 0x10 & 0xfffffff0),auVar48,
                                 (undefined1  [16])0x0);
            auVar39 = vectorMultiplyAddFloatingPoint
                                (*(undefined1 (*) [16])(uVar15 + 0x20 & 0xfffffff0),auVar50,auVar39)
            ;
            auVar50 = vectorMultiplyAddFloatingPoint
                                (*(undefined1 (*) [16])(uVar15 + 0x30 & 0xfffffff0),auVar47,auVar48)
            ;
            auVar48 = vectorAddFloatingPoint(auVar39,auVar50);
            auVar50 = vectorPermute(auVar4,auVar4,auVar43);
            auVar55 = vectorPermute(auVar4,auVar4,auVar44);
            auVar39 = vectorPermute(auVar4,auVar4,auVar46);
            auVar47 = vectorMultiplyAddFloatingPoint
                                (*(undefined1 (*) [16])(uVar15 & 0xfffffff0),auVar50,
                                 (undefined1  [16])0x0);
            auVar50 = vectorPermute(auVar4,auVar4,auVar45);
            auVar55 = vectorMultiplyAddFloatingPoint
                                (*(undefined1 (*) [16])(uVar15 + 0x10 & 0xfffffff0),auVar55,
                                 (undefined1  [16])0x0);
            auVar47 = vectorMultiplyAddFloatingPoint
                                (*(undefined1 (*) [16])(uVar15 + 0x20 & 0xfffffff0),auVar50,auVar47)
            ;
            auVar50 = vectorMultiplyAddFloatingPoint
                                (*(undefined1 (*) [16])(uVar15 + 0x30 & 0xfffffff0),auVar39,auVar55)
            ;
            auVar56 = vectorAddFloatingPoint(auVar47,auVar50);
            auVar50 = vectorPermute(auVar5,auVar5,auVar43);
            auVar55 = vectorPermute(auVar5,auVar5,auVar44);
            auVar39 = vectorPermute(auVar5,auVar5,auVar46);
            auVar47 = vectorMultiplyAddFloatingPoint
                                (*(undefined1 (*) [16])(uVar15 & 0xfffffff0),auVar50,
                                 (undefined1  [16])0x0);
            auVar50 = vectorPermute(auVar5,auVar5,auVar45);
            auVar55 = vectorMultiplyAddFloatingPoint
                                (*(undefined1 (*) [16])(uVar15 + 0x10 & 0xfffffff0),auVar55,
                                 (undefined1  [16])0x0);
            auVar47 = vectorMultiplyAddFloatingPoint
                                (*(undefined1 (*) [16])(uVar15 + 0x20 & 0xfffffff0),auVar50,auVar47)
            ;
            auVar50 = vectorMultiplyAddFloatingPoint
                                (*(undefined1 (*) [16])(uVar15 + 0x30 & 0xfffffff0),auVar39,auVar55)
            ;
            auVar57 = vectorAddFloatingPoint(auVar47,auVar50);
            auVar50 = vectorPermute(auStack_e0,auStack_e0,auVar43);
            auVar55 = vectorPermute(auStack_e0,auStack_e0,auVar44);
            auVar39 = vectorPermute(auStack_e0,auStack_e0,auVar46);
            auVar47 = vectorMultiplyAddFloatingPoint
                                (*(undefined1 (*) [16])(uVar15 & 0xfffffff0),auVar50,
                                 (undefined1  [16])0x0);
            auVar50 = vectorPermute(auStack_e0,auStack_e0,auVar45);
            auVar55 = vectorMultiplyAddFloatingPoint
                                (*(undefined1 (*) [16])(uVar15 + 0x10 & 0xfffffff0),auVar55,
                                 (undefined1  [16])0x0);
            auVar47 = vectorMultiplyAddFloatingPoint
                                (*(undefined1 (*) [16])(uVar15 + 0x20 & 0xfffffff0),auVar50,auVar47)
            ;
            auVar50 = vectorMultiplyAddFloatingPoint
                                (*(undefined1 (*) [16])(uVar15 + 0x30 & 0xfffffff0),auVar39,auVar55)
            ;
            auVar50 = vectorAddFloatingPoint(auVar47,auVar50);
            *(undefined1 (*) [16])(uVar26 - 0x1a0) = auVar50;
            local_1b0 = auVar48._0_4_;
            uStack_1ac = auVar48._4_4_;
            uStack_1a8 = auVar48._8_4_;
            uStack_1a4 = auVar48._12_4_;
            local_138[10] = local_1b0;
            local_10c = uStack_1ac;
            local_108 = uStack_1a8;
            local_104 = uStack_1a4;
            uStack_180 = auVar56._0_4_;
            uStack_17c = auVar56._4_4_;
            uStack_178 = auVar56._8_4_;
            uStack_174 = auVar56._12_4_;
            uStack_100 = uStack_180;
            uStack_fc = uStack_17c;
            uStack_f8 = uStack_178;
            uStack_f4 = uStack_174;
            uStack_190 = auVar57._0_4_;
            uStack_18c = auVar57._4_4_;
            uStack_188 = auVar57._8_4_;
            uStack_184 = auVar57._12_4_;
            uStack_f0 = uStack_190;
            uStack_ec = uStack_18c;
            uStack_e8 = uStack_188;
            uStack_e4 = uStack_184;
            auVar50._2_12_ = *(undefined1 (*) [12])(iVar24 + 0x10);
            auVar50[0xe] = 0x3f;
            auVar50[0xf] = 0x80;
            auVar50._0_2_ = 0;
            auVar50 = auVar50 << 0x10;
            auVar39 = vectorPermute(auVar50,auVar50,auVar43);
            auVar48 = vectorPermute(auVar50,auVar50,auVar44);
            auVar47 = vectorPermute(auVar50,auVar50,auVar46);
            auVar39 = vectorMultiplyAddFloatingPoint
                                (*(undefined1 (*) [16])(uVar15 & 0xfffffff0),auVar39,
                                 (undefined1  [16])0x0);
            auVar50 = vectorPermute(auVar50,auVar50,auVar45);
            auVar48 = vectorMultiplyAddFloatingPoint
                                (*(undefined1 (*) [16])(uVar15 + 0x10 & 0xfffffff0),auVar48,
                                 (undefined1  [16])0x0);
            auVar50 = vectorMultiplyAddFloatingPoint
                                (*(undefined1 (*) [16])(uVar15 + 0x20 & 0xfffffff0),auVar50,auVar39)
            ;
            auVar39 = vectorMultiplyAddFloatingPoint
                                (*(undefined1 (*) [16])(uVar15 + 0x30 & 0xfffffff0),auVar47,auVar48)
            ;
            auStack_e0 = vectorAddFloatingPoint(auVar50,auVar39);
            uVar15 = iVar32 * 0x40 + *(int *)(*(int *)(param_1 + iVar8) + 0xd4);
            puVar33 = (undefined4 *)(uVar15 & 0xfffffff0);
            *puVar33 = local_1b0;
            puVar33[1] = uStack_1ac;
            puVar33[2] = uStack_1a8;
            puVar33[3] = uStack_1a4;
            puVar33 = (undefined4 *)(uVar15 + 0x10 & 0xfffffff0);
            *puVar33 = uStack_180;
            puVar33[1] = uStack_17c;
            puVar33[2] = uStack_178;
            puVar33[3] = uStack_174;
            puVar33 = (undefined4 *)(uVar15 + 0x20 & 0xfffffff0);
            *puVar33 = uStack_190;
            puVar33[1] = uStack_18c;
            puVar33[2] = uStack_188;
            puVar33[3] = uStack_184;
            puVar33 = (undefined4 *)(uVar15 + 0x30 & 0xfffffff0);
            *puVar33 = auStack_e0._0_4_;
            puVar33[1] = auStack_e0._4_4_;
            puVar33[2] = auStack_e0._8_4_;
            puVar33[3] = auStack_e0._12_4_;
          }
          else {
            iVar18 = 0;
            lVar19 = ((ulonglong)*(uint *)(iVar24 + 0xac) - 1 & 0xffffffff) + 1;
            iVar27 = 0;
            if (*piVar28 != iVar20) {
              do {
                piVar28 = piVar28 + 0x14;
                iVar27 = iVar27 + 1;
                lVar19 = lVar19 + -1;
                if (lVar19 == 0) goto LAB_00e9a29c;
              } while (iVar20 != *piVar28);
              if (iVar27 < 0) goto LAB_00e9a29c;
              iVar18 = iVar27 * 0x40;
            }
            uVar23 = iVar18 + *(int *)(iVar24 + 0xd4);
            uVar15 = iVar32 * 0x40 + *(int *)(iVar7 + 0xd4);
            puVar33 = (undefined4 *)(uVar23 + 0x30 & 0xfffffff0);
            uVar35 = puVar33[1];
            uVar36 = puVar33[2];
            uVar37 = puVar33[3];
            puVar2 = (undefined4 *)(uVar23 & 0xfffffff0);
            uVar38 = *puVar2;
            uVar40 = puVar2[1];
            uVar41 = puVar2[2];
            uVar42 = puVar2[3];
            puVar2 = (undefined4 *)(uVar23 + 0x10 & 0xfffffff0);
            uVar54 = *puVar2;
            uVar58 = puVar2[1];
            uVar59 = puVar2[2];
            uVar60 = puVar2[3];
            puVar2 = (undefined4 *)(uVar23 + 0x20 & 0xfffffff0);
            uVar49 = *puVar2;
            uVar51 = puVar2[1];
            uVar52 = puVar2[2];
            uVar53 = puVar2[3];
            puVar2 = (undefined4 *)(uVar15 + 0x30 & 0xfffffff0);
            *puVar2 = *puVar33;
            puVar2[1] = uVar35;
            puVar2[2] = uVar36;
            puVar2[3] = uVar37;
            puVar33 = (undefined4 *)(uVar15 & 0xfffffff0);
            *puVar33 = uVar38;
            puVar33[1] = uVar40;
            puVar33[2] = uVar41;
            puVar33[3] = uVar42;
            puVar33 = (undefined4 *)(uVar15 + 0x10 & 0xfffffff0);
            *puVar33 = uVar54;
            puVar33[1] = uVar58;
            puVar33[2] = uVar59;
            puVar33[3] = uVar60;
            puVar33 = (undefined4 *)(uVar15 + 0x20 & 0xfffffff0);
            *puVar33 = uVar49;
            puVar33[1] = uVar51;
            puVar33[2] = uVar52;
            puVar33[3] = uVar53;
          }
          uVar15 = iVar32 + 1;
          iVar16 = iVar16 + 0x50;
          iVar32 = iVar32 + 1;
          iVar7 = *(int *)(param_1 + iVar8);
          local_c8 = (longlong)iVar8;
        } while (uVar15 < *(uint *)(iVar7 + 0xac));
      }
    }
    uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7bb0));
    _opd_FUN_00112098(local_c0,uVar11,0xf0);
    bVar1 = iVar30 != 0x24;
    iVar30 = iVar30 + 4;
  } while (bVar1);
  iVar30 = 0;
  if (*(int *)(param_1 + 0x444) != 0x8f) {
LAB_00e99778:
    do {
      iVar32 = iVar30 * 4;
      iVar7 = param_1 + iVar32 + 0x4a0;
      iVar13 = *(int *)(param_1 + iVar32 + 0x480);
      uVar11 = *(undefined4 *)(iVar7 + 8);
      *(undefined4 *)(iVar7 + 8) = uVar11;
      if (iVar13 != 0) {
        _opd_FUN_008d2bc0(iVar13,*(undefined4 *)(param_1 + iVar32 + 0x430),uVar11,
                          *(undefined4 *)(param_1 + 4));
        if (iVar30 == 3) {
          iVar13 = *(int *)(param_1 + 0x43c);
          if (iVar13 == 0x2e) {
            if ((ulonglong)*(uint *)(param_1 + 0x430) < 8) {
              uVar22 = 1L << ((ulonglong)*(uint *)(param_1 + 0x430) & 0x7f);
              if ((uVar22 & 0xc) == 0) {
                if ((uVar22 & 0x30) == 0) {
                  if ((uVar22 & 0xc0) == 0) goto LAB_00e99e3c;
                  if (*(int *)(param_1 + 4) == 0) {
                    iVar13 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fd4));
                  }
                  else {
                    iVar13 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fd0));
                  }
                }
                else if (*(int *)(param_1 + 4) == 0) {
                  iVar13 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fdc));
                }
                else {
                  iVar13 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fd8));
                }
              }
              else if (*(int *)(param_1 + 4) == 0) {
                iVar13 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fe4));
              }
              else {
                iVar13 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fe0));
              }
              if (iVar13 != 0) {
                uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fb4));
                iVar13 = _opd_FUN_001106e8(uVar11,iVar13);
                if ((iVar13 != 0) && (*(int *)(*(int *)(param_1 + 0x48c) + 0xb0) != 0)) {
                  uVar15 = 0;
                  piVar28 = (int *)(*(int *)(*(int *)(param_1 + 0x48c) + 0xe8) + 0x90);
                  do {
                    uVar15 = uVar15 + 1;
                    *piVar28 = iVar13;
                    piVar28 = piVar28 + 0x34;
                  } while (uVar15 < *(uint *)(*(int *)(param_1 + 0x48c) + 0xb0));
                  iVar30 = 4;
                  goto LAB_00e99778;
                }
              }
            }
          }
          else {
            bVar1 = iVar13 == 0x34;
            if ((bVar1) || (iVar13 == 0xa2)) {
              if ((ulonglong)*(uint *)(param_1 + 0x430) < 8) {
                dVar34 = (double)*(float *)(puVar6 + -0x7f48);
                fVar29 = 0.5;
                uVar22 = 1L << ((ulonglong)*(uint *)(param_1 + 0x430) & 0x7f);
                if ((uVar22 & 0xc) == 0) {
                  dVar34 = (double)*(float *)(puVar6 + -0x7f40);
                  fVar29 = 0.0;
                  if ((uVar22 & 0x30) == 0) {
                    fVar29 = 0.5;
                    local_a8 = 0.5;
                    dVar34 = 0.5;
                    if ((uVar22 & 0xc0) == 0) goto LAB_00e9a078;
                  }
                }
                if (bVar1) goto LAB_00e9a088;
LAB_00e99ea8:
                if (*(int *)(param_1 + 4) == 0) {
                  uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fc4));
                  uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fc0));
                }
                else {
                  uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fbc));
                  uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fb8));
                }
              }
              else {
LAB_00e9a078:
                fVar29 = 0.0;
                local_a8 = 0.0;
                dVar34 = 0.0;
                if (!bVar1) goto LAB_00e99ea8;
LAB_00e9a088:
                uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fcc));
                uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fc8));
              }
              iVar30 = 4;
              local_a8 = fVar29;
              _opd_FUN_00226b18(dVar34,(double)fVar29,uVar11,*(undefined4 *)(param_1 + 0x48c));
              local_a8 = fVar29;
              _opd_FUN_00226b18(dVar34,(double)fVar29,uVar12,*(undefined4 *)(param_1 + 0x48c));
              goto LAB_00e99778;
            }
          }
        }
        else if (iVar30 == 6) {
          if (*(int *)(param_1 + 0x448) == 0xc1) {
            if (*(int *)(param_1 + 4) == 0) {
              uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fb0));
            }
            else {
              uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fac));
            }
            cVar14 = _opd_FUN_0009c8b0();
            if (cVar14 == '\0') {
              uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fa8));
            }
            else {
              uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fa4));
            }
            iVar30 = 7;
            _opd_FUN_008d2920(*(undefined4 *)(param_1 + 0x498),uVar11,uVar12);
            goto LAB_00e99778;
          }
        }
        else {
          if (iVar30 != 1) goto LAB_00e997e4;
          iVar13 = *(int *)(param_1 + 0x434);
          if ((((iVar13 - 0xdU < 2) || (iVar13 == 0x10)) || (iVar13 == 0x11)) || (iVar13 == 0x12)) {
            if (*(int *)(param_1 + 4) == 0) {
              if ((ulonglong)*(uint *)(param_1 + 0x430) < 8) {
                uVar22 = 1L << ((ulonglong)*(uint *)(param_1 + 0x430) & 0x7f);
                if ((uVar22 & 0xc) == 0) {
                  if ((uVar22 & 0x30) == 0) {
                    if ((uVar22 & 0xc0) == 0) goto LAB_00e9a5b0;
                    local_138[10] = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7f94));
                  }
                  else {
                    local_138[10] = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7f98));
                  }
                }
                else {
                  local_138[10] = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7f9c));
                }
              }
              else {
LAB_00e9a5b0:
                local_138[10] = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fa0));
              }
              local_10c = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7f90));
              local_108 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7f8c));
              local_104 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7f88));
              uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7f84));
              local_170 = (undefined1)((uint)uVar11 >> 0x18);
              uStack_16f = (undefined1)((uint)uVar11 >> 0x10);
              uStack_16e = (undefined1)((uint)uVar11 >> 8);
              uStack_16d = (undefined1)uVar11;
              uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7f80));
              local_16c = (undefined1)((uint)uVar11 >> 0x18);
              uStack_16b = (undefined1)((uint)uVar11 >> 0x10);
              uStack_16a = (undefined1)((uint)uVar11 >> 8);
              uStack_169 = (undefined1)uVar11;
              uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7f7c));
              local_168 = (undefined1)((uint)uVar11 >> 0x18);
              uStack_167 = (undefined1)((uint)uVar11 >> 0x10);
              uStack_166 = (undefined1)((uint)uVar11 >> 8);
              uStack_165 = (undefined1)uVar11;
              uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7f78));
              local_164 = (undefined1)((uint)uVar11 >> 0x18);
              uStack_163 = (undefined1)((uint)uVar11 >> 0x10);
              uStack_162 = (undefined1)((uint)uVar11 >> 8);
              uStack_161 = (undefined1)uVar11;
            }
            else {
              if ((ulonglong)*(uint *)(param_1 + 0x430) < 8) {
                uVar22 = 1L << ((ulonglong)*(uint *)(param_1 + 0x430) & 0x7f);
                if ((uVar22 & 0xc) == 0) {
                  if ((uVar22 & 0x30) == 0) {
                    if ((uVar22 & 0xc0) == 0) goto LAB_00e9a6cc;
                    local_138[10] = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7f68));
                  }
                  else {
                    local_138[10] = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7f6c));
                  }
                }
                else {
                  local_138[10] = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7f70));
                }
              }
              else {
LAB_00e9a6cc:
                local_138[10] = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7f74));
              }
              local_10c = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7f64));
              local_108 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7f60));
              local_104 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7f5c));
              uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7f58));
              local_170 = (undefined1)((uint)uVar11 >> 0x18);
              uStack_16f = (undefined1)((uint)uVar11 >> 0x10);
              uStack_16e = (undefined1)((uint)uVar11 >> 8);
              uStack_16d = (undefined1)uVar11;
              uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7f54));
              local_16c = (undefined1)((uint)uVar11 >> 0x18);
              uStack_16b = (undefined1)((uint)uVar11 >> 0x10);
              uStack_16a = (undefined1)((uint)uVar11 >> 8);
              uStack_169 = (undefined1)uVar11;
              uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7f50));
              local_168 = (undefined1)((uint)uVar11 >> 0x18);
              uStack_167 = (undefined1)((uint)uVar11 >> 0x10);
              uStack_166 = (undefined1)((uint)uVar11 >> 8);
              uStack_165 = (undefined1)uVar11;
              uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7f4c));
              local_164 = (undefined1)((uint)uVar11 >> 0x18);
              uStack_163 = (undefined1)((uint)uVar11 >> 0x10);
              uStack_162 = (undefined1)((uint)uVar11 >> 8);
              uStack_161 = (undefined1)uVar11;
            }
            iVar30 = 2;
            _opd_FUN_008d2920(*(undefined4 *)(param_1 + 0x484),
                              CONCAT31(CONCAT21(CONCAT11(local_170,uStack_16f),uStack_16e),
                                       uStack_16d),local_138[10]);
            _opd_FUN_008d2920(*(undefined4 *)(param_1 + 0x484),
                              CONCAT31(CONCAT21(CONCAT11(local_16c,uStack_16b),uStack_16a),
                                       uStack_169),local_10c);
            _opd_FUN_008d2920(*(undefined4 *)(param_1 + 0x484),
                              CONCAT31(CONCAT21(CONCAT11(local_168,uStack_167),uStack_166),
                                       uStack_165),local_108);
            _opd_FUN_008d2920(*(undefined4 *)(param_1 + 0x484),
                              CONCAT31(CONCAT21(CONCAT11(local_164,uStack_163),uStack_162),
                                       uStack_161),local_104);
            goto LAB_00e99778;
          }
        }
LAB_00e99e3c:
        iVar30 = iVar30 + 1;
        goto LAB_00e99778;
      }
LAB_00e997e4:
      iVar30 = iVar30 + 1;
    } while (iVar30 < 10);
  }
  piVar28 = *(int **)(puVar6 + -0x7be4);
  if (*piVar28 != 0) {
    do {
      iVar30 = 0;
      puVar33 = (undefined4 *)(param_1 + 0x480);
      do {
        if (puVar33[-10] == *piVar28) {
          iVar13 = 0;
          do {
            if (*(int *)(iVar13 + param_1 + 0x458) == piVar28[1]) {
              if (piVar28[2] != 0) {
                _opd_FUN_00113560(*puVar33,piVar28[2]);
              }
              if (piVar28[8] != 0) {
                _opd_FUN_00113118(*puVar33,piVar28[8]);
              }
              if (piVar28[3] != 0) {
                _opd_FUN_00113560(*puVar33,piVar28[3]);
              }
              if (piVar28[9] != 0) {
                _opd_FUN_00113118(*puVar33,piVar28[9]);
              }
              if (piVar28[4] != 0) {
                _opd_FUN_00113560(*puVar33,piVar28[4]);
              }
              if (piVar28[10] != 0) {
                _opd_FUN_00113118(*puVar33,piVar28[10]);
              }
              if (piVar28[5] != 0) {
                _opd_FUN_00113560(*puVar33,piVar28[5]);
              }
              if (piVar28[0xb] != 0) {
                _opd_FUN_00113118(*puVar33,piVar28[0xb]);
              }
              if (piVar28[6] != 0) {
                _opd_FUN_00113560(*puVar33,piVar28[6]);
              }
              if (piVar28[0xc] != 0) {
                _opd_FUN_00113118(*puVar33,piVar28[0xc]);
              }
              if (piVar28[7] != 0) {
                _opd_FUN_00113560(*puVar33,piVar28[7]);
              }
              if (piVar28[0xd] != 0) {
                _opd_FUN_00113118(*puVar33,piVar28[0xd]);
              }
            }
            bVar1 = iVar13 != 0x24;
            iVar13 = iVar13 + 4;
          } while (bVar1);
        }
        bVar1 = iVar30 != 9;
        puVar33 = puVar33 + 1;
        iVar30 = iVar30 + 1;
      } while (bVar1);
      piVar28 = piVar28 + 0xe;
    } while (*piVar28 != 0);
  }
  if (*(int *)(param_1 + 4) == 0) {
    piVar28 = *(int **)(puVar6 + -0x7bac);
  }
  else {
    piVar28 = *(int **)(puVar6 + -0x7ba8);
  }
  if (*piVar28 != -1) {
    do {
      iVar30 = 0;
      puVar33 = (undefined4 *)(param_1 + 0x480);
      do {
        if (puVar33[-10] == *piVar28) {
          iVar13 = 0;
          do {
            if (*(int *)(iVar13 + param_1 + 0x458) == piVar28[1]) {
              iVar32 = 0;
              do {
                if (*(int *)(param_1 + iVar32 + 0x458) == piVar28[2]) {
                  if (piVar28[3] != 0) {
                    _opd_FUN_00113560(*puVar33,piVar28[3]);
                  }
                  if (piVar28[9] != 0) {
                    _opd_FUN_00113118(*puVar33,piVar28[9]);
                  }
                  if (piVar28[4] != 0) {
                    _opd_FUN_00113560(*puVar33,piVar28[4]);
                  }
                  if (piVar28[10] != 0) {
                    _opd_FUN_00113118(*puVar33,piVar28[10]);
                  }
                  if (piVar28[5] != 0) {
                    _opd_FUN_00113560(*puVar33,piVar28[5]);
                  }
                  if (piVar28[0xb] != 0) {
                    _opd_FUN_00113118(*puVar33,piVar28[0xb]);
                  }
                  if (piVar28[6] != 0) {
                    _opd_FUN_00113560(*puVar33,piVar28[6]);
                  }
                  if (piVar28[0xc] != 0) {
                    _opd_FUN_00113118(*puVar33,piVar28[0xc]);
                  }
                  if (piVar28[7] != 0) {
                    _opd_FUN_00113560(*puVar33,piVar28[7]);
                  }
                  if (piVar28[0xd] != 0) {
                    _opd_FUN_00113118(*puVar33,piVar28[0xd]);
                  }
                  if (piVar28[8] != 0) {
                    _opd_FUN_00113560(*puVar33,piVar28[8]);
                  }
                  if (piVar28[0xe] != 0) {
                    _opd_FUN_00113118(*puVar33,piVar28[0xe]);
                  }
                }
                bVar1 = iVar32 != 0x24;
                iVar32 = iVar32 + 4;
              } while (bVar1);
            }
            bVar1 = iVar13 != 0x24;
            iVar13 = iVar13 + 4;
          } while (bVar1);
        }
        bVar1 = iVar30 != 9;
        puVar33 = puVar33 + 1;
        iVar30 = iVar30 + 1;
      } while (bVar1);
      piVar28 = piVar28 + 0xf;
    } while (*piVar28 != -1);
  }
  uVar15 = *(uint *)(param_1 + 0x444);
  uVar22 = (ulonglong)uVar15 - 0x84;
  if (8 < (uVar22 & 0xffffffff)) {
    return;
  }
  if ((1L << (uVar22 & 0x7f) & 0x183U) == 0) {
    return;
  }
  iVar30 = *(int *)(param_1 + 0x494);
  if (iVar30 == 0) {
    return;
  }
  uVar22 = *(ulonglong *)(param_1 + 0xb48);
  uVar23 = (uint)(uVar22 >> 0x20);
  if ((uVar23 >> 0x1e & 1) == 0) {
    if ((longlong)uVar22 < 0) {
      uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fec));
      _opd_FUN_00113118(iVar30,uVar11);
      uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fe8));
      _opd_FUN_00113560(iVar30,uVar11);
      return;
    }
    uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fec));
    _opd_FUN_00113560(iVar30,uVar11);
    uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fe8));
    _opd_FUN_00113118(iVar30,uVar11);
    return;
  }
  if (0x83 < (int)uVar15) {
    if ((int)uVar15 < 0x86) {
      if (((*(int *)(param_1 + 0x450) != 0xf2) && (*(int *)(param_1 + 0x450) != 0x70)) &&
         (uVar26 = (ulonglong)*(uint *)(param_1 + 0x454) ^ 0x70,
         uVar31 = (ulonglong)((int)uVar26 >> 0x1f),
         uVar31 = (((uVar31 ^ uVar26) - uVar31) - 1 & 0xffffffff) >> 0x1f,
         *(uint *)(param_1 + 0x454) != 0xf2)) goto LAB_00e99988;
    }
    else {
      uVar31 = 0;
      if (((ulonglong)uVar15 - 0x8b & 0xffffffff) < 2) goto LAB_00e99988;
    }
  }
  uVar31 = 1;
LAB_00e99988:
  if ((uint)uVar31 != uVar23 >> 0x1f) {
    if ((uint)uVar31 == 0) {
      uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fec));
      _opd_FUN_00113560(iVar30,uVar11);
      uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fe8));
      _opd_FUN_00113118(iVar30,uVar11);
    }
    else {
      uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fec));
      _opd_FUN_00113118(iVar30,uVar11);
      uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fe8));
      _opd_FUN_00113560(iVar30,uVar11);
    }
    uVar22 = uVar31 << 0x3f | *(ulonglong *)(param_1 + 0xb48) & 0x7fffffffffffffff;
    *(ulonglong *)(param_1 + 0xb48) = uVar22;
  }
  *(ulonglong *)(param_1 + 0xb48) = uVar22 & 0xbfffffffffffffff;
  return;
}

