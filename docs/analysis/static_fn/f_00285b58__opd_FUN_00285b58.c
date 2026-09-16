/* containing function of static patch target(s); entry .opd.FUN_00285b58 @ 00285b58 */

void _opd_FUN_00285b58(int *param_1)

{
  bool bVar1;
  byte bVar2;
  ushort uVar3;
  bool bVar4;
  undefined *puVar5;
  int iVar6;
  uint *puVar7;
  int iVar8;
  uint uVar9;
  int iVar10;
  int iVar11;
  char cVar13;
  char cVar14;
  undefined4 *puVar12;
  uint uVar15;
  byte in_cr0;
  byte in_cr1;
  byte in_cr2;
  byte in_cr3;
  byte unaff_cr4;
  byte in_cr5;
  byte in_cr6;
  byte in_cr7;
  uint uStack00000008;
  undefined1 auStack_1d0 [8];
  undefined1 auStack_1c8 [32];
  undefined1 auStack_1a8 [12];
  undefined1 auStack_19c [276];
  
  puVar5 = PTR_PTR_0121af98;
  uStack00000008 =
       (uint)(in_cr0 & 0xf) << 0x1c | (uint)(in_cr1 & 0xf) << 0x18 | (uint)(in_cr2 & 0xf) << 0x14 |
       (uint)(in_cr3 & 0xf) << 0x10 | (uint)(unaff_cr4 & 0xf) << 0xc | (uint)(in_cr5 & 0xf) << 8 |
       (uint)(in_cr6 & 0xf) << 4 | (uint)(in_cr7 & 0xf);
  _opd_FUN_00263df0(param_1);
  if (*(short *)(param_1 + 0x1b9) < 0) {
    _opd_FUN_00269230(auStack_1c8,param_1[0xd]);
    while( true ) {
      puVar7 = (uint *)_opd_FUN_00269240(auStack_1c8);
      if (puVar7 == (uint *)0x0) break;
      *(byte *)((int)puVar7 + 9) = *(byte *)((int)puVar7 + 9) | 0x80;
      if (((*puVar7 & 0xfff) == (*(ushort *)((int)param_1 + 0x6e6) & 0xfff)) &&
         (*(char *)(puVar7 + 3) == *(char *)(param_1 + 0x1ba))) {
        *(ushort *)(param_1 + 0x1b9) = *(ushort *)(param_1 + 0x1b9) & 0x7fff;
        _opd_FUN_0026c9e0(param_1 + 0x1b6);
        break;
      }
      _opd_FUN_00269280(auStack_1c8);
    }
    _opd_FUN_00269ea8(param_1[0xd],0);
  }
  uVar3 = *(ushort *)param_1[0xd];
  uVar15 = (uint)((ushort *)param_1[0xd])[1];
  if (uVar3 >> 1 <= uVar15) {
    iVar6 = uVar15 - (uVar3 >> 2);
    _opd_FUN_00269230(auStack_1c8);
    while (puVar12 = (undefined4 *)_opd_FUN_00269240(auStack_1c8), puVar12 != (undefined4 *)0x0) {
      *(byte *)((int)puVar12 + 9) = *(byte *)((int)puVar12 + 9) | 0x80;
      iVar8 = _opd_FUN_00261438(*puVar12);
      if ((iVar8 != 0) && ((*(byte *)(iVar8 + 10) & 1) != 0)) {
        *(undefined1 *)(iVar8 + 0xc) = *(undefined1 *)(iVar8 + 0xb);
      }
      iVar6 = iVar6 - (uint)*(ushort *)((int)puVar12 + 6);
      if (iVar6 < 1) break;
      _opd_FUN_00269280(auStack_1c8);
    }
    _opd_FUN_00269ea8(param_1[0xd],0);
  }
  if ((-1 < *(short *)(param_1 + 0x1b9)) &&
     (uVar15 = _opd_FUN_0026c9f8(param_1 + 0x1b6), (uint)param_1[0x1b8] <= uVar15)) {
    iVar6 = 0;
    if (*(short *)(param_1[0xf] + 2) != 0xc) {
      iVar6 = _opd_FUN_0026d350(*(short *)(param_1[0xf] + 2),0);
      _opd_FUN_00f9ac38(iVar6,param_1[0xf],*(undefined2 *)(param_1[0xf] + 2));
      _opd_FUN_00269808(param_1[0xf]);
    }
    _opd_FUN_00269230(auStack_1d0,iVar6);
    puVar7 = (uint *)_opd_FUN_00269240(auStack_1d0);
    _opd_FUN_0026a0e8(auStack_1c8,(int)param_1 + 0x6e9,(*(ushort *)(param_1 + 0x1b9) & 0xfff) + 0xc)
    ;
    bVar1 = puVar7 == (uint *)0x0;
    iVar8 = _opd_FUN_00269448(auStack_1c8);
    while (iVar8 != 0) {
      uVar9 = _opd_FUN_002692c0(iVar8);
      uVar15 = uVar9 & 0xfff;
      iVar10 = _opd_FUN_002614f8(uVar15,param_1);
      if (iVar10 < 0) {
LAB_00285ea8:
        _opd_FUN_00269480(auStack_1c8);
      }
      else {
        while ((!bVar1 && ((*puVar7 & 0xfff) < uVar15))) {
          _opd_FUN_002696d8(param_1[0xf],puVar7,0);
          _opd_FUN_00269280(auStack_1d0);
          puVar7 = (uint *)_opd_FUN_00269240(auStack_1d0);
          bVar1 = puVar7 == (uint *)0x0;
        }
        iVar11 = _opd_FUN_00261438(iVar10);
        bVar2 = *(byte *)(iVar11 + 10);
        *(byte *)(iVar11 + 10) = bVar2 | 0x20;
        if ((uVar9 & 0x4000) != 0) goto LAB_00285ea8;
        if ((uVar9 & 0x1000) == 0) {
LAB_00286130:
          _opd_FUN_00269b00(param_1[0xf],iVar8,iVar10);
          _opd_FUN_00269480(auStack_1c8);
        }
        else {
          if (((bVar2 & 2) == 0) ||
             (cVar13 = *(char *)(iVar11 + 0xe), cVar14 = _opd_FUN_00269348(iVar8),
             (char)(cVar13 - cVar14) < '\x01')) {
            if ((!bVar1) &&
               (uVar9 = *puVar7, cVar13 = _opd_FUN_00269348(iVar8), uVar15 == (uVar9 & 0xfff))) {
              cVar14 = *(char *)((int)puVar7 + 10);
              bVar4 = false;
              if ((char)(cVar14 - cVar13) < '\x01') {
                do {
                  if (cVar13 == cVar14) {
                    bVar4 = true;
                    _opd_FUN_002696d8(param_1[0xf],puVar7,0);
                    _opd_FUN_00269280(auStack_1d0);
                    puVar7 = (uint *)_opd_FUN_00269240(auStack_1d0);
                  }
                  else {
                    _opd_FUN_002696d8(param_1[0xf],puVar7,0);
                    _opd_FUN_00269280(auStack_1d0);
                    puVar7 = (uint *)_opd_FUN_00269240(auStack_1d0);
                  }
                  bVar1 = puVar7 == (uint *)0x0;
                } while (((!bVar1) && ((*puVar7 & 0xfff) == uVar15)) &&
                        (cVar14 = *(char *)((int)puVar7 + 10), (char)(cVar14 - cVar13) < '\x01'));
                if (bVar4) goto LAB_00285ea8;
              }
            }
            goto LAB_00286130;
          }
          _opd_FUN_00269480(auStack_1c8);
        }
      }
      iVar8 = _opd_FUN_00269448(auStack_1c8);
    }
    _opd_FUN_00269668(auStack_1c8);
    if (!bVar1) {
      do {
        _opd_FUN_002696d8(param_1[0xf],puVar7,0);
        _opd_FUN_00269280(auStack_1d0);
        puVar7 = (uint *)_opd_FUN_00269240(auStack_1d0);
      } while (puVar7 != (uint *)0x0);
    }
    if (iVar6 != 0) {
      _opd_FUN_0026d250(iVar6);
    }
    *(ushort *)(param_1 + 5) =
         *(ushort *)(param_1 + 5) & 0xff80 |
         (ushort)((((ulonglong)*(ushort *)(param_1 + 5) & 0x3f) << 0x19) >> 0x10) >> 9;
    if (-1 < param_1[0x23b]) {
      puVar12 = *(undefined4 **)(puVar5 + -0x8000);
      iVar6 = (*(code *)**(undefined4 **)(*(int *)*puVar12 + 8))
                        ((int *)*puVar12,param_1[0x23b],auStack_1c8,9);
      if (5 < iVar6) {
        _opd_FUN_0026cad8(auStack_1a8,auStack_19c,0x100);
        _opd_FUN_0026caf0(auStack_1a8,auStack_1c8,iVar6);
        _opd_FUN_0026cc88(auStack_1a8,param_1 + 0x1b8,4);
        _opd_FUN_0026cc88(auStack_1a8,param_1 + 0x1b9,2);
        uVar15 = *(ushort *)(param_1 + 0x1b9) & 0xfff;
        if (uVar15 < 0x200) {
          if ((short)*(ushort *)(param_1 + 0x1b9) < 0) {
            _opd_FUN_0026cc88(auStack_1a8,(int)param_1 + 0x6e6,2);
            _opd_FUN_0026cc88(auStack_1a8,param_1 + 0x1ba,1);
            uVar9 = (*(code *)**(undefined4 **)(*(int *)*puVar12 + 8))
                              ((int *)*puVar12,param_1[0x23b],(int)param_1 + 0x6eb,uVar15);
            if (uVar15 == uVar9) {
              return;
            }
          }
          else {
            iVar6 = _opd_FUN_0026cb20(auStack_1a8,(int)param_1 + 0x6eb,3);
            iVar8 = (*(code *)**(undefined4 **)(*(int *)*puVar12 + 8))
                              ((int *)*puVar12,param_1[0x23b],(int)param_1 + iVar6 + 0x6eb,
                               uVar15 - iVar6);
            if (uVar15 - iVar6 == iVar8) {
              return;
            }
          }
        }
      }
      (*(code *)**(undefined4 **)(*(int *)*puVar12 + 4))((int *)*puVar12,param_1[0x23b]);
      *(undefined2 *)(param_1 + 0x1b9) = 0;
      param_1[0x23b] = -1;
      (*(code *)**(undefined4 **)(*param_1 + 8))(param_1);
    }
  }
  return;
}

