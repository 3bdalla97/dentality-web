import 'package:dental/domain/entities/category/category.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../blocs/category/category_bloc.dart';
import '../../../widgets/category_card.dart';

class SubCategoryView extends StatefulWidget {
  final Category category; // Add this line to receive the category

  const SubCategoryView({Key? key, required this.category}) : super(key: key);

  @override
  State<SubCategoryView> createState() => _SubCategoryViewState();
}

class _SubCategoryViewState extends State<SubCategoryView> {
  final TextEditingController _textEditingController = TextEditingController();

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 20),
        child: Column(
          children: [
            SizedBox(
              height: (MediaQuery.of(context).padding.top + 8),
            ),
            Padding(
              padding: const EdgeInsets.only(
                top: 12,
              ),
              child: TextField(
                controller: _textEditingController,
                autofocus: false,
                onSubmitted: (val) {
                  context.read<CategoryBloc>().add(FilterCategories(val));
                },
                onChanged: (val) => setState(() {
                  context.read<CategoryBloc>().add(FilterCategories(val));
                }),
                decoration: InputDecoration(
                    contentPadding:
                        const EdgeInsets.only(left: 20, bottom: 22, top: 22),
                    prefixIcon: const Padding(
                      padding: EdgeInsets.only(left: 8),
                      child: Icon(Icons.search),
                    ),
                    suffixIcon: _textEditingController.text.isNotEmpty
                        ? Padding(
                            padding: const EdgeInsets.only(right: 8),
                            child: IconButton(
                                onPressed: () {
                                  setState(() {
                                    _textEditingController.clear();
                                    context
                                        .read<CategoryBloc>()
                                        .add(const FilterCategories(''));
                                  });
                                },
                                icon: const Icon(Icons.clear)),
                          )
                        : null,
                    border: const OutlineInputBorder(),
                    hintText: "Search Sub Category",
                    fillColor: Colors.grey.shade100,
                    filled: true,
                    focusedBorder: OutlineInputBorder(
                        borderSide:
                            const BorderSide(color: Colors.white, width: 3.0),
                        borderRadius: BorderRadius.circular(26)),
                    enabledBorder: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(26),
                      borderSide:
                          const BorderSide(color: Colors.white, width: 3.0),
                    )),
              ),
            ),
            Expanded(
              child: BlocBuilder<CategoryBloc, CategoryState>(
                builder: (context, state) {
                  if (state is CategoryLoading) {
                    return ListView.builder(
                      itemCount: 10,
                      physics: const BouncingScrollPhysics(),
                      padding: EdgeInsets.only(
                          top: 14,
                          bottom: (80 + MediaQuery.of(context).padding.bottom)),
                      itemBuilder: (context, index) => const CategoryCard(),
                    );
                  } else if (state is CategoryLoaded) {
                    final subCategories = widget.category.subcategories;
                    if (subCategories == null || subCategories.isEmpty) {
                      return const Center(
                          child: Text('No subcategories found'));
                    }
                    return ListView.builder(
                      itemCount: subCategories.length,
                      physics: const BouncingScrollPhysics(),
                      padding: EdgeInsets.only(
                          top: 14,
                          bottom: (80 + MediaQuery.of(context).padding.bottom)),
                      itemBuilder: (context, index) {
                        final subCategory =
                            subCategories.values.elementAt(index);
                        return CategoryCard(
                          category: subCategory,
                        );
                      },
                    );
                  } else {
                    return const Center(child: Text('No subcategories found'));
                  }
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}
