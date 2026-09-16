/* containing function of static patch target(s); entry .opd.FUN_00aa38ec @ 00aa38ec */

longlong _opd_FUN_00aa38ec(undefined4 param_1)

{
  bool bVar1;
  undefined *puVar2;
  int iVar4;
  longlong lVar3;
  undefined4 uVar5;
  int iVar6;
  undefined4 uVar7;
  uint *puVar8;
  int iVar9;
  undefined4 uVar10;
  undefined4 uVar11;
  undefined4 uVar12;
  undefined4 uVar13;
  undefined4 uVar14;
  undefined4 uVar15;
  undefined *puVar16;
  int iVar17;
  int *piVar18;
  undefined8 uVar19;
  
  puVar2 = PTR_PTR_0121bc30;
  uVar19 = *(undefined8 *)(PTR_PTR_0121bc30 + -0x7ff0);
  iVar4 = _opd_FUN_000be778(0xc1ac0,uVar19);
  if (iVar4 != 0) {
    lVar3 = _opd_FUN_000bead8(iVar4,uVar19,*(undefined4 *)(puVar2 + -0x7fe8),
                              *(undefined4 *)(puVar2 + -0x7fe4));
    if (lVar3 != 0) {
      iVar9 = 0;
      **(int **)(puVar2 + -0x8000) = iVar4;
      uVar5 = _opd_FUN_00280418();
      *(undefined4 *)(iVar4 + 0x60) = uVar5;
      uVar5 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fe0));
      piVar18 = (int *)(iVar4 + 0xc1a90);
      do {
        iVar6 = _opd_FUN_00242400(uVar5,1,0,0x50);
        *piVar18 = iVar6;
        if (iVar6 == 0) goto LAB_00aa43c4;
        uVar7 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fdc));
        puVar8 = (uint *)_opd_FUN_00242bf0(iVar6,uVar7);
        bVar1 = iVar9 != 0x20;
        iVar9 = iVar9 + 4;
        if ((puVar8 != (uint *)0x0) && ((*puVar8 >> 0x16 & 1) != 0)) {
          *puVar8 = *puVar8 & 0xffbfffff | 0x40000000;
        }
        piVar18 = piVar18 + 1;
      } while (bVar1);
      uVar5 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fd8));
      iVar9 = _opd_FUN_00242400(uVar5,1,0,0x46);
      *(int *)(iVar4 + 0xc1ab4) = iVar9;
      if (iVar9 != 0) {
        uVar5 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fdc));
        puVar8 = (uint *)_opd_FUN_00242bf0(iVar9,uVar5);
        if ((puVar8 != (uint *)0x0) && ((*puVar8 >> 0x16 & 1) != 0)) {
          *puVar8 = *puVar8 & 0xffbfffff | 0x40000000;
        }
        iVar9 = _opd_FUN_0097c124();
        _opd_FUN_00a0ed68(*(undefined4 *)(iVar9 + 0x2a4),0x8b,0x8c);
        iVar17 = iVar4 + 0x6c;
        iVar6 = _opd_FUN_0097c124();
        uVar5 = *(undefined4 *)(iVar6 + 0x2a4);
        uVar7 = _opd_FUN_009745d0(*(undefined4 *)(puVar2 + -0x7fd4));
        _opd_FUN_00a0ca3c(uVar5,0x69838a,uVar7);
        uVar5 = *(undefined4 *)(iVar9 + 0x2a4);
        uVar7 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fd0));
        _opd_FUN_0024ae50(uVar5,uVar7);
        iVar9 = _opd_FUN_0097c124();
        uVar5 = *(undefined4 *)(iVar4 + 0xc1ab4);
        uVar7 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fcc));
        uVar10 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fc8));
        uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fc4));
        uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fc0));
        uVar13 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fbc));
        uVar14 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fb8));
        _opd_FUN_00aa9268(iVar17,uVar5,uVar7,uVar10,uVar11,uVar12,uVar13,uVar14);
        uVar5 = *(undefined4 *)(puVar2 + -0x7fb4);
        uVar7 = *(undefined4 *)(iVar4 + 0xc1a90);
        uVar12 = _opd_FUN_000cdc90(uVar5);
        uVar10 = *(undefined4 *)(iVar9 + 0x2a4);
        uVar13 = _opd_FUN_000cdc90(uVar5);
        uVar5 = *(undefined4 *)(puVar2 + -0x7fb0);
        uVar14 = _opd_FUN_000cdc90(uVar5);
        uVar11 = *(undefined4 *)(puVar2 + -0x7fac);
        uVar15 = _opd_FUN_000cdc90(uVar11);
        _opd_FUN_00aa8f7c(iVar17,uVar7,uVar10,uVar12,uVar10,uVar13,uVar14,uVar15);
        uVar7 = *(undefined4 *)(puVar2 + -0x7fa8);
        uVar10 = *(undefined4 *)(iVar4 + 0xc1a94);
        uVar13 = _opd_FUN_000cdc90(uVar7);
        uVar12 = *(undefined4 *)(iVar9 + 0x2a4);
        uVar7 = _opd_FUN_000cdc90(uVar7);
        uVar14 = _opd_FUN_000cdc90(uVar5);
        uVar15 = _opd_FUN_000cdc90(uVar11);
        _opd_FUN_00aa8f7c(iVar17,uVar10,uVar12,uVar13,uVar12,uVar7,uVar14,uVar15);
        uVar7 = *(undefined4 *)(puVar2 + -0x7fa4);
        uVar10 = *(undefined4 *)(iVar4 + 0xc1a98);
        uVar13 = _opd_FUN_000cdc90(uVar7);
        uVar12 = *(undefined4 *)(iVar9 + 0x2a4);
        uVar7 = _opd_FUN_000cdc90(uVar7);
        uVar14 = _opd_FUN_000cdc90(uVar5);
        uVar15 = _opd_FUN_000cdc90(uVar11);
        _opd_FUN_00aa8f7c(iVar17,uVar10,uVar12,uVar13,uVar12,uVar7,uVar14,uVar15);
        uVar7 = *(undefined4 *)(puVar2 + -0x7fa0);
        uVar10 = *(undefined4 *)(iVar4 + 0xc1a9c);
        uVar13 = _opd_FUN_000cdc90(uVar7);
        uVar12 = *(undefined4 *)(iVar9 + 0x2a4);
        uVar7 = _opd_FUN_000cdc90(uVar7);
        uVar14 = _opd_FUN_000cdc90(uVar5);
        uVar15 = _opd_FUN_000cdc90(uVar11);
        _opd_FUN_00aa8f7c(iVar17,uVar10,uVar12,uVar13,uVar12,uVar7,uVar14,uVar15);
        uVar7 = *(undefined4 *)(puVar2 + -0x7f9c);
        uVar10 = *(undefined4 *)(iVar4 + 0xc1aa0);
        uVar13 = _opd_FUN_000cdc90(uVar7);
        uVar12 = *(undefined4 *)(iVar9 + 0x2a4);
        uVar7 = _opd_FUN_000cdc90(uVar7);
        uVar14 = _opd_FUN_000cdc90(uVar5);
        uVar15 = _opd_FUN_000cdc90(uVar11);
        _opd_FUN_00aa8f7c(iVar17,uVar10,uVar12,uVar13,uVar12,uVar7,uVar14,uVar15);
        uVar7 = *(undefined4 *)(puVar2 + -0x7f98);
        uVar10 = *(undefined4 *)(iVar4 + 0xc1aa4);
        uVar13 = _opd_FUN_000cdc90(uVar7);
        uVar12 = *(undefined4 *)(iVar9 + 0x2a4);
        uVar7 = _opd_FUN_000cdc90(uVar7);
        uVar14 = _opd_FUN_000cdc90(uVar5);
        uVar15 = _opd_FUN_000cdc90(uVar11);
        _opd_FUN_00aa8f7c(iVar17,uVar10,uVar12,uVar13,uVar12,uVar7,uVar14,uVar15);
        uVar7 = *(undefined4 *)(puVar2 + -0x7f94);
        uVar10 = *(undefined4 *)(iVar4 + 0xc1aa8);
        uVar13 = _opd_FUN_000cdc90(uVar7);
        uVar12 = *(undefined4 *)(iVar9 + 0x2a4);
        uVar7 = _opd_FUN_000cdc90(uVar7);
        uVar14 = _opd_FUN_000cdc90(uVar5);
        uVar15 = _opd_FUN_000cdc90(uVar11);
        _opd_FUN_00aa8f7c(iVar17,uVar10,uVar12,uVar13,uVar12,uVar7,uVar14,uVar15);
        uVar7 = *(undefined4 *)(puVar2 + -0x7f90);
        uVar10 = *(undefined4 *)(iVar4 + 0xc1aac);
        uVar13 = _opd_FUN_000cdc90(uVar7);
        uVar12 = *(undefined4 *)(iVar9 + 0x2a4);
        uVar7 = _opd_FUN_000cdc90(uVar7);
        uVar14 = _opd_FUN_000cdc90(uVar5);
        uVar15 = _opd_FUN_000cdc90(uVar11);
        _opd_FUN_00aa8f7c(iVar17,uVar10,uVar12,uVar13,uVar12,uVar7,uVar14,uVar15);
        uVar7 = *(undefined4 *)(puVar2 + -0x7f8c);
        uVar10 = *(undefined4 *)(iVar4 + 0xc1ab0);
        uVar13 = _opd_FUN_000cdc90(uVar7);
        uVar12 = *(undefined4 *)(iVar9 + 0x2a4);
        uVar7 = _opd_FUN_000cdc90(uVar7);
        uVar5 = _opd_FUN_000cdc90(uVar5);
        uVar11 = _opd_FUN_000cdc90(uVar11);
        _opd_FUN_00aa8f7c(iVar17,uVar10,uVar12,uVar13,uVar12,uVar7,uVar5,uVar11);
        iVar9 = _opd_FUN_00a1fea0();
        if ((iVar9 == 0) || (iVar9 = _opd_FUN_00a1fea0(), iVar9 == 1)) {
          uVar5 = _opd_FUN_00a0c930(0x8d);
          uVar7 = _opd_FUN_00a0c930(0x8e);
          _opd_FUN_00aa8920(iVar17,uVar5,uVar7,*(undefined4 *)(puVar2 + -0x7f88));
        }
        uVar5 = _opd_FUN_00a0c930(0xb1);
        uVar7 = _opd_FUN_00a0c930(0xb2);
        _opd_FUN_00aa8920(iVar17,uVar5,uVar7,*(undefined4 *)(puVar2 + -0x7f84));
        uVar5 = _opd_FUN_00a0c930(0x8f);
        uVar7 = _opd_FUN_00a0c930(0x94);
        _opd_FUN_00aa8920(iVar17,uVar5,uVar7,*(undefined4 *)(puVar2 + -0x7f80));
        *(undefined4 *)(iVar17 + *(int *)(iVar4 + 0xc1a38) * 0x318 + -0x10) = 1;
        uVar5 = _opd_FUN_00a0c930(0x90);
        uVar7 = _opd_FUN_00a0c930(0x95);
        _opd_FUN_00aa8920(iVar17,uVar5,uVar7,*(undefined4 *)(puVar2 + -0x7f7c));
        *(undefined4 *)(iVar17 + *(int *)(iVar4 + 0xc1a38) * 0x318 + -0x10) = 1;
        uVar5 = _opd_FUN_00a0c930(0x91);
        uVar7 = _opd_FUN_00a0c930(0x96);
        _opd_FUN_00aa8920(iVar17,uVar5,uVar7,*(undefined4 *)(puVar2 + -0x7f78));
        *(undefined4 *)(iVar17 + *(int *)(iVar4 + 0xc1a38) * 0x318 + -0x10) = 1;
        uVar5 = _opd_FUN_00a0c930(0x9d);
        uVar7 = _opd_FUN_00a0c930(0x9e);
        _opd_FUN_00aa8920(iVar17,uVar5,uVar7,*(undefined4 *)(puVar2 + -0x7f74));
        *(undefined4 *)(iVar17 + *(int *)(iVar4 + 0xc1a38) * 0x318 + -0x10) = 2;
        uVar5 = _opd_FUN_00a0c930(0x92);
        uVar7 = _opd_FUN_00a0c930(0x97);
        _opd_FUN_00aa8920(iVar17,uVar5,uVar7,*(undefined4 *)(puVar2 + -0x7f70));
        *(undefined4 *)(iVar17 + *(int *)(iVar4 + 0xc1a38) * 0x318 + -0x10) = 1;
        _opd_FUN_00aaacc8(iVar17,param_1);
        iVar9 = *(int *)(iVar17 + *(int *)(iVar4 + 0xc1a30) * 0x318 + 0x308);
        if (iVar9 == 1) {
          uVar5 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7f6c));
          puVar16 = &DAT_00fcf82b;
        }
        else if (iVar9 == 2) {
          uVar5 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7f68));
          puVar16 = (undefined *)0xb94d4c;
        }
        else {
          uVar5 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fcc));
          puVar16 = (undefined *)0xb8cd4c;
        }
        _opd_FUN_00aa81dc(iVar17,puVar16,uVar5);
        if (*(longlong *)(iVar4 + 0xc1a88) == 0) {
          uVar19 = _opd_FUN_009f7cc8(*(undefined4 *)(iVar4 + 0xc1a2c),0x10c2d1);
          *(undefined8 *)(iVar4 + 0xc1a88) = uVar19;
          _opd_FUN_000beea8(iVar4,uVar19);
        }
        if (*(int *)(iVar17 + *(int *)(iVar4 + 0xc1a30) * 0x318 + 0x308) == 1) {
          _opd_FUN_00a88ca8();
        }
        else {
          _opd_FUN_00a88c18();
        }
        iVar9 = 0;
        piVar18 = (int *)(iVar4 + 0xc1a90);
        do {
          if (*piVar18 != 0) {
            _opd_FUN_0024d078((double)*(float *)(puVar2 + -0x7f64),*piVar18);
          }
          bVar1 = iVar9 != 0x20;
          iVar9 = iVar9 + 4;
          piVar18 = piVar18 + 1;
        } while (bVar1);
        if (*(int *)(iVar4 + 0xc1ab4) != 0) {
          _opd_FUN_0024d078((double)*(float *)(puVar2 + -0x7f64),*(int *)(iVar4 + 0xc1ab4));
        }
        piVar18 = *(int **)(puVar2 + -0x8000);
        _opd_FUN_00f18b60(*(undefined4 *)(*piVar18 + 0x60));
        _opd_FUN_00f0f088(*(undefined4 *)(*piVar18 + 0x60));
        _opd_FUN_00f1eee0(*(undefined4 *)(*piVar18 + 0x60));
        _opd_FUN_00f2c448(*(undefined4 *)(*piVar18 + 0x60));
        _opd_FUN_00f0f0c8(*(undefined4 *)(*piVar18 + 0x60));
        *(undefined2 *)(*piVar18 + 0x68) = 0;
        return lVar3;
      }
    }
LAB_00aa43c4:
    _opd_FUN_000c1e00(iVar4);
  }
  return 0;
}

