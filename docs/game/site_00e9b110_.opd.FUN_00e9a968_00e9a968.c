// hook SITE 0x00e9b110

undefined4 _opd_FUN_00e9a968(int param_1,undefined *param_2,undefined4 *param_3)

{
  bool bVar1;
  int iVar2;
  undefined4 *puVar3;
  undefined4 *puVar4;
  undefined *puVar5;
  undefined4 uVar6;
  uint uVar7;
  byte *pbVar8;
  uint uVar11;
  longlong lVar9;
  ulonglong uVar10;
  int iVar12;
  int iVar13;
  uint uVar14;
  int iVar15;
  int iVar16;
  int *piVar17;
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
  undefined1 local_e0 [16];
  undefined1 local_d0 [16];
  undefined1 local_c0 [88];
  
  puVar5 = PTR_PTR_0121c31c;
  if (param_2 == &UNK_003d0006) {
    *param_3 = *(undefined4 *)(param_1 + 0x10);
    return 1;
  }
  if (param_2 < &UNK_003d0007) {
    if (param_2 == &DAT_003d0003) {
      *(undefined4 **)(param_1 + 0x9b0) = param_3;
      if (param_3 == (undefined4 *)0xffffffff) {
        return 1;
      }
      _opd_FUN_0060cfb8(*(undefined4 *)(param_1 + 0x10),param_1 + 0x930,
                        *(undefined4 *)(param_1 + 0x9bc),param_3,0,local_c0);
      return 1;
    }
    if (&DAT_003d0004 <= param_2) {
      if (param_2 == &DAT_003d0004) {
        _opd_FUN_0016abf0(param_1 + 0x310);
        return 1;
      }
      if (param_2 != &DAT_003d0005) {
        return 0;
      }
      return 0xffffffff;
    }
    if (param_2 != &UNK_003d0001) {
      if (param_2 != &UNK_003d0002) {
        return 0;
      }
      if (*(undefined4 **)(param_1 + 0xa30) == param_3) {
        return 1;
      }
      _opd_FUN_00cd1270(param_1 + 0x10,*(undefined4 *)(param_1 + 0x9b4),0,param_3,0,local_e0,0,0x100
                       );
      _opd_FUN_00d4e0f0(*(undefined4 *)(param_1 + 0x14),0);
      *(undefined4 **)(param_1 + 0xa30) = param_3;
      *(uint *)(param_1 + 0xa34) = *(uint *)(param_1 + 0xa34) & 0xfffffffe;
      return 1;
    }
    piVar17 = (int *)(param_1 + 0x480);
    _opd_FUN_00e991e8(param_1,param_3);
    iVar16 = 0;
    iVar12 = *(int *)(param_1 + 0x10);
    iVar15 = 0x4d0;
    do {
      iVar2 = *piVar17;
      if (iVar2 != 0) {
        if (piVar17[0x122] == 0) {
          if (*(int *)(iVar2 + 0xac) != 0) {
            uVar7 = 0;
            pbVar8 = (byte *)(param_1 + iVar15);
            do {
              iVar13 = uVar7 * 0x40;
              uVar7 = uVar7 + 1;
              uVar14 = iVar13 + *(int *)(iVar2 + 0xd4);
              uVar11 = (uint)*pbVar8 * 0x40 + *(int *)(iVar12 + 0xd4);
              puVar3 = (undefined4 *)(uVar11 + 0x30 & 0xfffffff0);
              uVar6 = puVar3[1];
              uVar18 = puVar3[2];
              uVar19 = puVar3[3];
              puVar4 = (undefined4 *)(uVar11 & 0xfffffff0);
              uVar20 = *puVar4;
              uVar21 = puVar4[1];
              uVar22 = puVar4[2];
              uVar23 = puVar4[3];
              puVar4 = (undefined4 *)(uVar11 + 0x10 & 0xfffffff0);
              uVar28 = *puVar4;
              uVar29 = puVar4[1];
              uVar30 = puVar4[2];
              uVar31 = puVar4[3];
              puVar4 = (undefined4 *)(uVar11 + 0x20 & 0xfffffff0);
              uVar24 = *puVar4;
              uVar25 = puVar4[1];
              uVar26 = puVar4[2];
              uVar27 = puVar4[3];
              puVar4 = (undefined4 *)(uVar14 + 0x30 & 0xfffffff0);
              *puVar4 = *puVar3;
              puVar4[1] = uVar6;
              puVar4[2] = uVar18;
              puVar4[3] = uVar19;
              puVar3 = (undefined4 *)(uVar14 & 0xfffffff0);
              *puVar3 = uVar20;
              puVar3[1] = uVar21;
              puVar3[2] = uVar22;
              puVar3[3] = uVar23;
              puVar3 = (undefined4 *)(uVar14 + 0x10 & 0xfffffff0);
              *puVar3 = uVar28;
              puVar3[1] = uVar29;
              puVar3[2] = uVar30;
              puVar3[3] = uVar31;
              puVar3 = (undefined4 *)(uVar14 + 0x20 & 0xfffffff0);
              *puVar3 = uVar24;
              puVar3[1] = uVar25;
              puVar3[2] = uVar26;
              puVar3[3] = uVar27;
              pbVar8 = pbVar8 + 1;
            } while (uVar7 < *(uint *)(iVar2 + 0xac));
          }
          uVar7 = *(uint *)(iVar2 + 0xd4);
          puVar3 = (undefined4 *)(uVar7 + 0x30 & 0xfffffff0);
          uVar24 = *puVar3;
          uVar25 = puVar3[1];
          uVar26 = puVar3[2];
          uVar27 = puVar3[3];
          puVar3 = (undefined4 *)(uVar7 & 0xfffffff0);
          uVar6 = puVar3[1];
          uVar18 = puVar3[2];
          uVar19 = puVar3[3];
          puVar4 = (undefined4 *)(uVar7 + 0x10 & 0xfffffff0);
          uVar20 = *puVar4;
          uVar21 = puVar4[1];
          uVar22 = puVar4[2];
          uVar23 = puVar4[3];
          puVar4 = (undefined4 *)(uVar7 + 0x20 & 0xfffffff0);
          uVar28 = *puVar4;
          uVar29 = puVar4[1];
          uVar30 = puVar4[2];
          uVar31 = puVar4[3];
          puVar4 = (undefined4 *)(iVar2 + 0x10U & 0xfffffff0);
          *puVar4 = *puVar3;
          puVar4[1] = uVar6;
          puVar4[2] = uVar18;
          puVar4[3] = uVar19;
          puVar3 = (undefined4 *)(iVar2 + 0x20U & 0xfffffff0);
          *puVar3 = uVar20;
          puVar3[1] = uVar21;
          puVar3[2] = uVar22;
          puVar3[3] = uVar23;
          puVar3 = (undefined4 *)(iVar2 + 0x30U & 0xfffffff0);
          *puVar3 = uVar28;
          puVar3[1] = uVar29;
          puVar3[2] = uVar30;
          puVar3[3] = uVar31;
          puVar3 = (undefined4 *)(iVar2 + 0x40U & 0xfffffff0);
          *puVar3 = uVar24;
          puVar3[1] = uVar25;
          puVar3[2] = uVar26;
          puVar3[3] = uVar27;
        }
        *(int *)(iVar2 + 0x54) = param_1 + 0x80;
      }
      bVar1 = iVar16 != 9;
      iVar15 = iVar15 + 100;
      piVar17 = piVar17 + 1;
      iVar16 = iVar16 + 1;
    } while (bVar1);
    iVar12 = 0;
    if ((*(uint *)(param_1 + 0xa14) & 1) != 0) {
LAB_00e9b21c:
      lVar9 = (ulonglong)(9 - iVar12) + 1;
      if (10 < iVar12 + 1U) {
        lVar9 = 1;
      }
      do {
        iVar15 = *(int *)(param_1 + iVar12 * 4 + 0x480);
        if ((iVar15 != 0) &&
           (*(uint *)(iVar15 + 0x5c) = *(uint *)(iVar15 + 0x5c) & 0xfffffff8,
           *(int *)(param_1 + 0xa48) - 1U < 2)) {
          if (iVar12 == 5) {
            iVar15 = *(int *)(param_1 + 0x444);
            if ((iVar15 != 0x2b) && ((iVar15 != 0x83 && (iVar15 != 0x8e)))) goto code_r0x00e9b2c8;
          }
          else if ((iVar12 < 5) || (7 < iVar12)) goto LAB_00e9b264;
          iVar15 = *(int *)(param_1 + iVar12 * 4 + 0x480);
          *(uint *)(iVar15 + 0x5c) = *(uint *)(iVar15 + 0x5c) | 7;
        }
LAB_00e9b264:
        iVar12 = iVar12 + 1;
        lVar9 = lVar9 + -1;
        if (lVar9 == 0) {
          return 1;
        }
      } while( true );
    }
  }
  else {
    if (param_2 != &UNK_003d0009) {
      if (param_2 < &DAT_003d000a) {
        if (param_2 == &UNK_003d0007) {
          if (*(undefined4 **)(param_1 + 0xa30) == param_3) {
            return 1;
          }
          _opd_FUN_00cd1270(param_1 + 0x10,*(undefined4 *)(param_1 + 0x9b4),0,param_3,0,local_d0,0,
                            0x100);
          _opd_FUN_00d4e098(*(undefined4 *)(param_1 + 0x14),0);
          *(undefined4 **)(param_1 + 0xa30) = param_3;
          *(uint *)(param_1 + 0xa34) = *(uint *)(param_1 + 0xa34) | 1;
          return 1;
        }
        if (param_2 != &UNK_003d0008) {
          return 0;
        }
        if ((*(uint *)(param_1 + 0xa34) & 1) == 0) {
          if ((*(uint *)(*(int *)(*(int *)(param_1 + 0x14) + 4) + 4) & 1) != 0) {
            *param_3 = 1;
            return 1;
          }
          *param_3 = 0;
          return 1;
        }
        *param_3 = 0xffffffff;
        return 1;
      }
      if (param_2 == (undefined *)0xfffe0001) {
        *(uint *)(param_1 + 0xa14) = *(uint *)(param_1 + 0xa14) | 1;
        return 1;
      }
      if (param_2 == (undefined *)0xfffe0002) {
        *(uint *)(param_1 + 0xa14) = *(uint *)(param_1 + 0xa14) & 0xfffffffe;
        return 1;
      }
      if (param_2 != &DAT_003d0016) {
        return 0;
      }
      iVar12 = *(int *)(param_1 + 0x494);
      if (iVar12 == 0) {
        return 1;
      }
      uVar10 = (ulonglong)*(uint *)(param_1 + 0x444) - 0x84;
      if (8 < (uVar10 & 0xffffffff)) {
        return 1;
      }
      if ((1L << (uVar10 & 0x7f) & 0x183U) == 0) {
        return 1;
      }
      uVar10 = *(ulonglong *)(param_1 + 0xb48);
      if ((longlong)uVar10 < 0) {
        uVar6 = *(undefined4 *)(PTR_PTR_0121c31c + -0x7fec);
        *(ulonglong *)(param_1 + 0xb48) = uVar10 & 0x7fffffffffffffff;
        uVar6 = _opd_FUN_000cdc90(uVar6);
        _opd_FUN_00113560(iVar12,uVar6);
        uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7fe8));
        _opd_FUN_00113118(iVar12,uVar6);
      }
      else {
        uVar6 = *(undefined4 *)(PTR_PTR_0121c31c + -0x7fec);
        *(ulonglong *)(param_1 + 0xb48) = uVar10 | 0x8000000000000000;
        uVar6 = _opd_FUN_000cdc90(uVar6);
        _opd_FUN_00113118(iVar12,uVar6);
        uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar5 + -0x7fe8));
        _opd_FUN_00113560(iVar12,uVar6);
      }
      _opd_FUN_000482b0(0x22a);
      return 1;
    }
    piVar17 = (int *)(param_1 + 0x480);
    *(undefined4 *)(param_1 + 0x4a8) = *param_3;
    *(undefined4 *)(param_1 + 0x4ac) = param_3[1];
    *(undefined4 *)(param_1 + 0x4b0) = param_3[2];
    iVar16 = 0x4d0;
    *(undefined4 *)(param_1 + 0x4b4) = param_3[3];
    *(undefined4 *)(param_1 + 0x4b8) = param_3[4];
    *(undefined4 *)(param_1 + 0x4bc) = param_3[5];
    *(undefined4 *)(param_1 + 0x4c0) = param_3[6];
    *(undefined4 *)(param_1 + 0x4c4) = param_3[7];
    *(undefined4 *)(param_1 + 0x4c8) = param_3[8];
    *(undefined4 *)(param_1 + 0x4cc) = param_3[9];
    _opd_FUN_00e991e8(param_1,param_1 + 0x430);
    iVar15 = 0;
    iVar12 = *(int *)(param_1 + 0x10);
    do {
      iVar2 = *piVar17;
      if (iVar2 != 0) {
        if (piVar17[0x122] == 0) {
          if (*(int *)(iVar2 + 0xac) != 0) {
            uVar7 = 0;
            pbVar8 = (byte *)(param_1 + iVar16);
            do {
              iVar13 = uVar7 * 0x40;
              uVar7 = uVar7 + 1;
              uVar14 = iVar13 + *(int *)(iVar2 + 0xd4);
              uVar11 = (uint)*pbVar8 * 0x40 + *(int *)(iVar12 + 0xd4);
              puVar3 = (undefined4 *)(uVar11 + 0x30 & 0xfffffff0);
              uVar6 = puVar3[1];
              uVar18 = puVar3[2];
              uVar19 = puVar3[3];
              puVar4 = (undefined4 *)(uVar11 & 0xfffffff0);
              uVar20 = *puVar4;
              uVar21 = puVar4[1];
              uVar22 = puVar4[2];
              uVar23 = puVar4[3];
              puVar4 = (undefined4 *)(uVar11 + 0x10 & 0xfffffff0);
              uVar28 = *puVar4;
              uVar29 = puVar4[1];
              uVar30 = puVar4[2];
              uVar31 = puVar4[3];
              puVar4 = (undefined4 *)(uVar11 + 0x20 & 0xfffffff0);
              uVar24 = *puVar4;
              uVar25 = puVar4[1];
              uVar26 = puVar4[2];
              uVar27 = puVar4[3];
              puVar4 = (undefined4 *)(uVar14 + 0x30 & 0xfffffff0);
              *puVar4 = *puVar3;
              puVar4[1] = uVar6;
              puVar4[2] = uVar18;
              puVar4[3] = uVar19;
              puVar3 = (undefined4 *)(uVar14 & 0xfffffff0);
              *puVar3 = uVar20;
              puVar3[1] = uVar21;
              puVar3[2] = uVar22;
              puVar3[3] = uVar23;
              puVar3 = (undefined4 *)(uVar14 + 0x10 & 0xfffffff0);
              *puVar3 = uVar28;
              puVar3[1] = uVar29;
              puVar3[2] = uVar30;
              puVar3[3] = uVar31;
              puVar3 = (undefined4 *)(uVar14 + 0x20 & 0xfffffff0);
              *puVar3 = uVar24;
              puVar3[1] = uVar25;
              puVar3[2] = uVar26;
              puVar3[3] = uVar27;
              pbVar8 = pbVar8 + 1;
            } while (uVar7 < *(uint *)(iVar2 + 0xac));
          }
          uVar7 = *(uint *)(iVar2 + 0xd4);
          puVar3 = (undefined4 *)(uVar7 + 0x30 & 0xfffffff0);
          uVar24 = *puVar3;
          uVar25 = puVar3[1];
          uVar26 = puVar3[2];
          uVar27 = puVar3[3];
          puVar3 = (undefined4 *)(uVar7 & 0xfffffff0);
          uVar6 = puVar3[1];
          uVar18 = puVar3[2];
          uVar19 = puVar3[3];
          puVar4 = (undefined4 *)(uVar7 + 0x10 & 0xfffffff0);
          uVar20 = *puVar4;
          uVar21 = puVar4[1];
          uVar22 = puVar4[2];
          uVar23 = puVar4[3];
          puVar4 = (undefined4 *)(uVar7 + 0x20 & 0xfffffff0);
          uVar28 = *puVar4;
          uVar29 = puVar4[1];
          uVar30 = puVar4[2];
          uVar31 = puVar4[3];
          puVar4 = (undefined4 *)(iVar2 + 0x10U & 0xfffffff0);
          *puVar4 = *puVar3;
          puVar4[1] = uVar6;
          puVar4[2] = uVar18;
          puVar4[3] = uVar19;
          puVar3 = (undefined4 *)(iVar2 + 0x20U & 0xfffffff0);
          *puVar3 = uVar20;
          puVar3[1] = uVar21;
          puVar3[2] = uVar22;
          puVar3[3] = uVar23;
          puVar3 = (undefined4 *)(iVar2 + 0x30U & 0xfffffff0);
          *puVar3 = uVar28;
          puVar3[1] = uVar29;
          puVar3[2] = uVar30;
          puVar3[3] = uVar31;
          puVar3 = (undefined4 *)(iVar2 + 0x40U & 0xfffffff0);
          *puVar3 = uVar24;
          puVar3[1] = uVar25;
          puVar3[2] = uVar26;
          puVar3[3] = uVar27;
        }
        *(int *)(iVar2 + 0x54) = param_1 + 0x80;
      }
      bVar1 = iVar15 != 9;
      iVar16 = iVar16 + 100;
      piVar17 = piVar17 + 1;
      iVar15 = iVar15 + 1;
    } while (bVar1);
    iVar12 = 0;
    if ((*(uint *)(param_1 + 0xa14) & 1) != 0) {
LAB_00e9ad94:
      lVar9 = (ulonglong)(9 - iVar12) + 1;
      if (10 < iVar12 + 1U) {
        lVar9 = 1;
      }
      do {
        iVar15 = *(int *)(param_1 + iVar12 * 4 + 0x480);
        if ((iVar15 != 0) &&
           (*(uint *)(iVar15 + 0x5c) = *(uint *)(iVar15 + 0x5c) & 0xfffffff8,
           *(int *)(param_1 + 0xa48) - 1U < 2)) {
          if (iVar12 == 5) {
            iVar15 = *(int *)(param_1 + 0x444);
            if (((iVar15 != 0x2b) && (iVar15 != 0x83)) && (iVar15 != 0x8e)) goto code_r0x00e9af64;
          }
          else if ((iVar12 < 5) || (7 < iVar12)) goto LAB_00e9ae18;
          iVar15 = *(int *)(param_1 + iVar12 * 4 + 0x480);
          *(uint *)(iVar15 + 0x5c) = *(uint *)(iVar15 + 0x5c) | 7;
        }
LAB_00e9ae18:
        iVar12 = iVar12 + 1;
        lVar9 = lVar9 + -1;
        if (lVar9 == 0) {
          return 1;
        }
      } while( true );
    }
  }
  iVar12 = *(int *)(param_1 + 0x480);
  if (iVar12 != 0) {
    *(uint *)(iVar12 + 0x5c) = *(uint *)(iVar12 + 0x5c) | 7;
  }
  iVar12 = *(int *)(param_1 + 0x484);
  if (iVar12 != 0) {
    *(uint *)(iVar12 + 0x5c) = *(uint *)(iVar12 + 0x5c) | 7;
  }
  iVar12 = *(int *)(param_1 + 0x488);
  if (iVar12 != 0) {
    *(uint *)(iVar12 + 0x5c) = *(uint *)(iVar12 + 0x5c) | 7;
  }
  iVar12 = *(int *)(param_1 + 0x48c);
  if (iVar12 != 0) {
    *(uint *)(iVar12 + 0x5c) = *(uint *)(iVar12 + 0x5c) | 7;
  }
  iVar12 = *(int *)(param_1 + 0x490);
  if (iVar12 != 0) {
    *(uint *)(iVar12 + 0x5c) = *(uint *)(iVar12 + 0x5c) | 7;
  }
  iVar12 = *(int *)(param_1 + 0x494);
  if (iVar12 != 0) {
    *(uint *)(iVar12 + 0x5c) = *(uint *)(iVar12 + 0x5c) | 7;
  }
  iVar12 = *(int *)(param_1 + 0x498);
  if (iVar12 != 0) {
    *(uint *)(iVar12 + 0x5c) = *(uint *)(iVar12 + 0x5c) | 7;
  }
  iVar12 = *(int *)(param_1 + 0x49c);
  if (iVar12 != 0) {
    *(uint *)(iVar12 + 0x5c) = *(uint *)(iVar12 + 0x5c) | 7;
  }
  iVar12 = *(int *)(param_1 + 0x4a0);
  if (iVar12 != 0) {
    *(uint *)(iVar12 + 0x5c) = *(uint *)(iVar12 + 0x5c) | 7;
  }
  iVar12 = *(int *)(param_1 + 0x4a4);
  if (iVar12 == 0) {
    return 1;
  }
  *(uint *)(iVar12 + 0x5c) = *(uint *)(iVar12 + 0x5c) | 7;
  return 1;
code_r0x00e9b2c8:
  iVar12 = 6;
  goto LAB_00e9b21c;
code_r0x00e9af64:
  iVar12 = 6;
  goto LAB_00e9ad94;
}

