// hook SITE 0x00262528

void _opd_FUN_002620d8(void)

{
  bool bVar1;
  bool bVar2;
  ushort uVar3;
  int *piVar4;
  undefined4 uVar5;
  undefined4 uVar6;
  undefined *puVar7;
  int iVar8;
  undefined4 uVar9;
  int iVar10;
  char cVar12;
  int iVar11;
  int iVar13;
  int *piVar14;
  int *piVar15;
  byte in_cr0;
  byte in_cr1;
  byte in_cr2;
  byte in_cr3;
  byte unaff_cr4;
  byte in_cr5;
  byte in_cr6;
  byte in_cr7;
  uint uStack00000008;
  undefined1 local_b0 [4];
  undefined1 auStack_ac [8];
  undefined1 auStack_a4 [2];
  short local_a2;
  
  puVar7 = PTR_PTR_0121af44;
  uStack00000008 =
       (uint)(in_cr0 & 0xf) << 0x1c | (uint)(in_cr1 & 0xf) << 0x18 | (uint)(in_cr2 & 0xf) << 0x14 |
       (uint)(in_cr3 & 0xf) << 0x10 | (uint)(unaff_cr4 & 0xf) << 0xc | (uint)(in_cr5 & 0xf) << 8 |
       (uint)(in_cr6 & 0xf) << 4 | (uint)(in_cr7 & 0xf);
  piVar4 = *(int **)(PTR_PTR_0121af44 + -0x8000);
  iVar13 = 0;
  iVar8 = _opd_FUN_0026c9f8(piVar4 + 4);
  piVar4[2] = iVar8;
  _opd_FUN_0026c9e0(piVar4 + 4);
  piVar15 = (int *)piVar4[10];
  do {
    if (1 < *(byte *)(piVar15 + 1)) {
      (*(code *)**(undefined4 **)(*piVar15 + 0x34))(piVar15);
    }
    bVar1 = iVar13 != 0x17;
    *(undefined2 *)(piVar15 + 0x30) = 0;
    iVar13 = iVar13 + 1;
    piVar15 = piVar15 + 0x1b6;
  } while (bVar1);
  piVar15 = (int *)piVar4[0xb];
  if (piVar15 != (int *)0x0) {
    (*(code *)**(undefined4 **)(*piVar15 + 0x34))(piVar15);
  }
  piVar15 = (int *)piVar4[0xc];
  if (piVar15 != (int *)0x0) {
    (*(code *)**(undefined4 **)(*piVar15 + 0x34))(piVar15);
  }
  if (*piVar4 == 2) {
    uVar9 = _opd_FUN_0026d350(0x578,0);
LAB_002622c4:
    iVar8 = FUN_00fbb59c(piVar4[1],uVar9,0x578,0x80,auStack_a4,local_b0);
    if (0 < iVar8) {
      while( true ) {
        iVar13 = piVar4[0x10];
        piVar4[0x17] = iVar8 + piVar4[0x17] + 0x2e;
        if ((iVar13 != 0) && (iVar11 = _opd_FUN_00fa4e20(auStack_ac,piVar4 + 0x10,4), iVar11 == 0))
        break;
        piVar15 = (int *)piVar4[10];
        iVar11 = 0;
        uVar5 = *(undefined4 *)(puVar7 + -0x7ff0);
        uVar6 = *(undefined4 *)(puVar7 + -0x7fec);
        bVar1 = false;
        piVar14 = piVar15;
        do {
          if (1 < *(byte *)(piVar14 + 1)) {
            uVar3 = *(ushort *)(piVar14 + 5);
            if ((uVar3 & 4) == 0) {
              bVar1 = true;
            }
            else {
              iVar10 = _opd_FUN_00fa4e20(piVar14 + 0xb,auStack_ac,4);
              if ((iVar10 == 0) && (*(short *)(piVar14 + 0xc) == local_a2)) {
                if ((iVar13 != 0) && ((uVar3 & 0x100) == 0)) {
                  _opd_FUN_0026ca78(uVar5,uVar6);
                }
                uVar3 = *(ushort *)piVar14[0xf];
                if ((uint)((int)((uint)uVar3 * 2 + (uint)uVar3) >> 2) <
                    (uint)((ushort *)piVar14[0xf])[1]) {
                  (*(code *)**(undefined4 **)(*piVar14 + 0x34))(piVar14);
                }
                _opd_FUN_002666c8(piVar14,uVar9,iVar8);
                if ((byte)(*(char *)(piVar14 + 1) - 2U) < 5) {
                  _opd_FUN_00268ba8(piVar14,auStack_ac);
                }
                if (iVar11 != 0x18) goto LAB_002622c4;
                goto LAB_00262418;
              }
            }
          }
          bVar2 = iVar11 != 0x17;
          piVar14 = piVar14 + 0x1b6;
          iVar11 = iVar11 + 1;
        } while (bVar2);
        if (bVar1) {
          iVar13 = 0;
          do {
            if (((1 < *(byte *)(piVar15 + 1)) && (*(char *)((int)piVar15 + 5) != '\x02')) &&
               ((*(ushort *)(piVar15 + 5) & 4) == 0)) {
              cVar12 = _opd_FUN_002666c8(piVar15,uVar9,iVar8);
              if ((cVar12 != '\0') &&
                 ((6 < *(byte *)(piVar15 + 1) ||
                  (cVar12 = _opd_FUN_00268ba8(piVar15,auStack_ac), cVar12 != '\0'))))
              goto LAB_002622c4;
            }
            bVar1 = iVar13 != 0x17;
            iVar13 = iVar13 + 1;
            piVar15 = piVar15 + 0x1b6;
          } while (bVar1);
        }
LAB_00262418:
        iVar13 = piVar4[10];
        if (((*(char *)(iVar13 + 0x9d6d) != '\x02') || (*(byte *)(iVar13 + 0x9d6c) < 2)) ||
           ((iVar8 = _opd_FUN_00267d58(iVar13 + 0x9d68,uVar9,iVar8,auStack_ac), iVar8 == 0 ||
            ((undefined4 *)piVar4[0x14] == (undefined4 *)0x0)))) goto LAB_002622c4;
        (**(code **)piVar4[0x14])(iVar8);
        iVar8 = FUN_00fbb59c(piVar4[1],uVar9,0x578,0x80,auStack_a4,local_b0);
        if (iVar8 < 1) goto LAB_002624b8;
      }
      _opd_FUN_0026c9f8(piVar4 + 0x12);
      goto LAB_002622c4;
    }
LAB_002624b8:
    _opd_FUN_0026d250(uVar9);
  }
  return;
}

