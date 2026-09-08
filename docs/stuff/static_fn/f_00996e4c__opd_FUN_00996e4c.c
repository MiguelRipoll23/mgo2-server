/* containing function of static patch target(s); entry .opd.FUN_00996e4c @ 00996e4c */

longlong _opd_FUN_00996e4c(void)

{
  bool bVar1;
  undefined *puVar2;
  int iVar4;
  longlong lVar3;
  undefined4 uVar5;
  int iVar6;
  int iVar7;
  undefined4 uVar8;
  uint *puVar9;
  undefined4 uVar10;
  undefined4 uVar11;
  undefined4 uVar12;
  undefined4 uVar13;
  undefined4 uVar14;
  undefined4 uVar15;
  undefined8 uVar16;
  int *piVar17;
  undefined8 uVar18;
  
  puVar2 = PTR_PTR_0121bafc;
  uVar18 = *(undefined8 *)(PTR_PTR_0121bafc + -0x7ff8);
  iVar4 = _opd_FUN_000be778(0xc1aa0,uVar18);
  if (iVar4 != 0) {
    lVar3 = _opd_FUN_000bead8(iVar4,uVar18,*(undefined4 *)(puVar2 + -0x7ff0),
                              *(undefined4 *)(puVar2 + -0x7fec));
    if (lVar3 != 0) {
      **(int **)(puVar2 + -0x8000) = iVar4;
      uVar5 = _opd_FUN_00280418();
      *(undefined4 *)(iVar4 + 0x60) = uVar5;
      iVar6 = _opd_FUN_00242400(0xf5e395,1,0,0x3c);
      *(int *)(iVar4 + 0xc1a80) = iVar6;
      if (iVar6 != 0) {
        iVar6 = 0;
        uVar5 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fe8));
        piVar17 = (int *)(iVar4 + 0xc1a84);
        do {
          iVar7 = _opd_FUN_00242400(uVar5,1,0,0x50);
          *piVar17 = iVar7;
          if (iVar7 == 0) goto LAB_00997640;
          uVar8 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fe4));
          puVar9 = (uint *)_opd_FUN_00242bf0(iVar7,uVar8);
          bVar1 = iVar6 != 0xc;
          iVar6 = iVar6 + 4;
          if ((puVar9 != (uint *)0x0) && ((*puVar9 >> 0x16 & 1) != 0)) {
            *puVar9 = *puVar9 & 0xffbfffff | 0x40000000;
          }
          piVar17 = piVar17 + 1;
        } while (bVar1);
        uVar5 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fe0));
        iVar6 = _opd_FUN_00242400(uVar5,1,0,0x46);
        *(int *)(iVar4 + 0xc1a94) = iVar6;
        if (iVar6 != 0) {
          uVar5 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fe4));
          puVar9 = (uint *)_opd_FUN_00242bf0(iVar6,uVar5);
          if ((puVar9 != (uint *)0x0) && ((*puVar9 >> 0x16 & 1) != 0)) {
            *puVar9 = *puVar9 & 0xffbfffff | 0x40000000;
          }
          iVar6 = _opd_FUN_0097c124();
          iVar7 = _opd_FUN_00243908(*(undefined4 *)(iVar6 + 0x2a4));
          if (iVar7 != 0) {
            puVar9 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar6 + 0x2a4),0x2a4634);
            if ((puVar9 != (uint *)0x0) && ((*puVar9 >> 0x16 & 1) != 0)) {
              *puVar9 = *puVar9 & 0xffbfffff | 0x40000000;
            }
            _opd_FUN_002438e0(*(undefined4 *)(iVar6 + 0x2a4));
          }
          iVar6 = iVar4 + 0x68;
          _opd_FUN_00a0ed68(*(undefined4 *)(iVar4 + 0xc1a80),0x6a,0x6b);
          uVar5 = *(undefined4 *)(iVar4 + 0xc1a80);
          uVar8 = _opd_FUN_009745d0(*(undefined4 *)(puVar2 + -0x7fdc));
          _opd_FUN_00a0ca3c(uVar5,0x698386,uVar8);
          _opd_FUN_00eace20(*(undefined4 *)(iVar4 + 0xc1a80),0x6a367f,0);
          _opd_FUN_00eace20(*(undefined4 *)(iVar4 + 0xc1a80),0x6742c5,0);
          uVar5 = *(undefined4 *)(iVar4 + 0xc1a94);
          uVar8 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fd8));
          uVar10 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fd4));
          uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fd0));
          uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fcc));
          uVar13 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fc8));
          uVar14 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fc4));
          _opd_FUN_00aa9268(iVar6,uVar5,uVar8,uVar10,uVar11,uVar12,uVar13,uVar14);
          uVar5 = *(undefined4 *)(iVar4 + 0xc1a84);
          uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fc0));
          uVar8 = *(undefined4 *)(iVar4 + 0xc1a80);
          uVar13 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fbc));
          uVar10 = *(undefined4 *)(puVar2 + -0x7fb8);
          uVar14 = _opd_FUN_000cdc90(uVar10);
          uVar11 = *(undefined4 *)(puVar2 + -0x7fb4);
          uVar15 = _opd_FUN_000cdc90(uVar11);
          _opd_FUN_00aa8f7c(iVar6,uVar5,uVar8,uVar12,uVar8,uVar13,uVar14,uVar15);
          uVar5 = *(undefined4 *)(iVar4 + 0xc1a88);
          uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fb0));
          uVar8 = *(undefined4 *)(iVar4 + 0xc1a80);
          uVar13 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fac));
          uVar14 = _opd_FUN_000cdc90(uVar10);
          uVar15 = _opd_FUN_000cdc90(uVar11);
          _opd_FUN_00aa8f7c(iVar6,uVar5,uVar8,uVar12,uVar8,uVar13,uVar14,uVar15);
          uVar5 = *(undefined4 *)(iVar4 + 0xc1a8c);
          uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fa8));
          uVar8 = *(undefined4 *)(iVar4 + 0xc1a80);
          uVar13 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fa4));
          uVar14 = _opd_FUN_000cdc90(uVar10);
          uVar15 = _opd_FUN_000cdc90(uVar11);
          _opd_FUN_00aa8f7c(iVar6,uVar5,uVar8,uVar12,uVar8,uVar13,uVar14,uVar15);
          uVar5 = *(undefined4 *)(iVar4 + 0xc1a90);
          uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fa0));
          uVar8 = *(undefined4 *)(iVar4 + 0xc1a80);
          uVar13 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7f9c));
          uVar10 = _opd_FUN_000cdc90(uVar10);
          uVar11 = _opd_FUN_000cdc90(uVar11);
          _opd_FUN_00aa8f7c(iVar6,uVar5,uVar8,uVar12,uVar8,uVar13,uVar10,uVar11);
          uVar5 = _opd_FUN_00a0c930(0x75);
          uVar8 = _opd_FUN_00a0c930(0x76);
          _opd_FUN_00aa8920(iVar6,uVar5,uVar8,*(undefined4 *)(puVar2 + -0x7f98));
          *(undefined4 *)(iVar6 + *(int *)(iVar4 + 0xc1a34) * 0x318 + -8) = 0;
          uVar5 = _opd_FUN_00a0c930(0x6e);
          uVar8 = _opd_FUN_00a0c930(0x70);
          _opd_FUN_00aa8920(iVar6,uVar5,uVar8,*(undefined4 *)(puVar2 + -0x7f94));
          *(undefined4 *)(iVar6 + *(int *)(iVar4 + 0xc1a34) * 0x318 + -0x10) = 1;
          *(undefined4 *)(iVar6 + *(int *)(iVar4 + 0xc1a34) * 0x318 + -8) = 0;
          uVar5 = _opd_FUN_00a0c930(0x8f);
          uVar8 = _opd_FUN_00a0c930(0x94);
          _opd_FUN_00aa8920(iVar6,uVar5,uVar8,*(undefined4 *)(puVar2 + -0x7f90));
          *(undefined4 *)(iVar6 + *(int *)(iVar4 + 0xc1a34) * 0x318 + -0x10) = 1;
          uVar5 = _opd_FUN_00a0c930(0x6f);
          uVar8 = _opd_FUN_00a0c930(0x71);
          _opd_FUN_00aa8920(iVar6,uVar5,uVar8,*(undefined4 *)(puVar2 + -0x7f8c));
          uVar18 = 0xba79cc;
          uVar16 = 0x6b2a5e;
          *(undefined4 *)(iVar6 + *(int *)(iVar4 + 0xc1a34) * 0x318 + -0x10) = 1;
          if (*(int *)(iVar6 + *(int *)(iVar4 + 0xc1a2c) * 0x318 + 0x308) != 1) {
            uVar18 = 0x90e758;
            uVar16 = 0x9bf073;
          }
          _opd_FUN_00aa81dc(iVar6,uVar18,uVar16);
          _opd_FUN_00a0ca3c(*(undefined4 *)(iVar4 + 0xc1a80),0x698386,
                            *(int *)(iVar4 + 0xc1a2c) * 0x318 + iVar6 + 0x181);
          if (*(longlong *)(iVar4 + 0xc1a78) == 0) {
            uVar18 = _opd_FUN_009f7cc8(*(undefined4 *)(iVar4 + 0xc1a94),0x10c2d1);
            *(undefined8 *)(iVar4 + 0xc1a78) = uVar18;
            _opd_FUN_000beea8(iVar4,uVar18);
          }
          if (*(int *)(iVar6 + *(int *)(iVar4 + 0xc1a2c) * 0x318 + 0x308) == 1) {
            _opd_FUN_00a88ca8();
          }
          else {
            _opd_FUN_00a88c18();
          }
          if (*(int *)(iVar4 + 0xc1a80) != 0) {
            _opd_FUN_0024d078((double)*(float *)(puVar2 + -0x7f88),*(int *)(iVar4 + 0xc1a80));
          }
          iVar6 = 0;
          piVar17 = (int *)(iVar4 + 0xc1a84);
          do {
            if (*piVar17 != 0) {
              _opd_FUN_0024d078((double)*(float *)(puVar2 + -0x7f80),*piVar17);
            }
            bVar1 = iVar6 != 0xc;
            iVar6 = iVar6 + 4;
            piVar17 = piVar17 + 1;
          } while (bVar1);
          if (*(int *)(iVar4 + 0xc1a94) != 0) {
            _opd_FUN_0024d078((double)*(float *)(puVar2 + -0x7f80),*(int *)(iVar4 + 0xc1a94));
          }
          *(undefined2 *)(**(int **)(puVar2 + -0x8000) + 100) = 0;
          return lVar3;
        }
      }
    }
LAB_00997640:
    _opd_FUN_000c1e00(iVar4);
  }
  return 0;
}

